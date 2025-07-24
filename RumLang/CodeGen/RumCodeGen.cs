using System.Diagnostics;
using QbeGenerator;
using RumLang.Analyzer;
using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.TopologicalSort;

namespace RumLang.CodeGen;

public enum CodeGenError
{
    Success,
    Error,
}

public class RumCodeGen(Rum rum, List<AstNode> astNodes, RumAnalyzer analyzer)
{
    private Dictionary<LiteralExpression, QbeGlobalRef> _stringLiterals = new();
    private Dictionary<string, (AnalyzerType, QbeType)> _types = new();
    private Dictionary<QbeType, List<QbeFunction>> _functions = new();
    private List<QbeFunction> _globalFunctions = new();
    private ExpressionConverter _expressionConverter;
    
    public (string outputCode, CodeGenError codegenError, long ms) GenerateCode()
    {
        Stopwatch sw = Stopwatch.StartNew();
        var strings = analyzer.StringLiterals;

        List<AstNode> nodes = new();
        
        AdjacencyGraph<AnalyzerNamespace, Edge<AnalyzerNamespace>> graph = new();
        
        // Add all the external dependencies to the graph
        var externalEntries = analyzer.ExternalDependencies;
        var allNamespaces = externalEntries
            .SelectMany(de => de.ExternalEntry.Namespaces)
            .ToList();
        
        foreach (var ns in allNamespaces)
            graph.AddVertex(ns);
        
        foreach (var dep in externalEntries)
        {
            var imports = dep.ExternalEntry.Imports
                .Select(im => im.Name)               // string identifiers
                .Distinct();

            foreach (var ns in dep.ExternalEntry.Namespaces)
            {
                foreach (var importedName in imports)
                {
                    var providers = allNamespaces
                        .Where(p => p.GetFullName() == importedName);

                    foreach (var provider in providers)
                    {
                        graph.AddEdge(new Edge<AnalyzerNamespace>(provider, ns));
                    }
                }
            }
        }
        var sorter = new TopologicalSortAlgorithm<AnalyzerNamespace, Edge<AnalyzerNamespace>>(graph);
        sorter.Compute();
        var sortedNamespaces = sorter.SortedVertices as List<AnalyzerNamespace>;
        foreach (var ns in sortedNamespaces)
        {
            var entry = externalEntries
                .First(de => de.ExternalEntry.Namespaces.Contains(ns));

            nodes.AddRange(entry.ExternalEntry.Nodes);
        }
        
        // Now we add our own defined namespaces, functions, and types
        nodes.AddRange(astNodes); 

        var module = new QbeModule(false); // if true, 32 bit mode, if false, 64 bit mode

        AddStrings(module, strings);
        
        AddTypes(module, sortedNamespaces);
        AddTypes(module, analyzer.DefinedTypes);

        _expressionConverter =
            new ExpressionConverter(_blockStack, null, _stringLiterals, _types, _functions, _globalFunctions, analyzer.Options);
        
        AddTypeDefinitions(module, sortedNamespaces);
        AddTypeDefinitions(module, analyzer.DefinedTypes);
        
        // Add all the functions.
        foreach (var func in analyzer.DefinedFunctions)
        {
            AddFunction(module, null, func.Value);
        }
        
        // Now add all the function bodies.
        foreach (var body in _functionBodies)
        {
            body();
        }
        
        // Now we sort the external dependencies by their order, so if
        string output = module.Emit();
        sw.Stop();
        return (output, CodeGenError.Success, sw.ElapsedMilliseconds);
    }

    private void AddTypeDefinitions(QbeModule module, List<AnalyzerNamespace> analyzerDefinedTypes)
    {
        // This is done in a separate method, because otherwise a type might not be defined yet when we try to add a field to it.
        foreach (var ns in analyzerDefinedTypes)
        {
            var prefix = ns.GetFullName();
            foreach (var type in ns.Types)
            {
                var qbeType = _types[$"{prefix}.{type.Name}"].Item2;
                foreach (var field in type.Members)
                {
                    AddField(module, qbeType, field);
                }
                
                foreach (var function in type.Functions)
                {
                    AddFunction(module, qbeType, function);
                }
            }
        }
    }

    private void AddTypeDefinitions(QbeModule module, Dictionary<string, AnalyzerType> analyzerDefinedTypes)
    {
        // This is done in a separate method, because otherwise a type might not be defined yet when we try to add a field to it.
        foreach (var ns in analyzerDefinedTypes)
        {
            var qbeType = _types[ns.Key].Item2;
            foreach (var field in ns.Value.Members)
            {
                AddField(module, qbeType, field);
            }
            
            foreach (var function in ns.Value.Functions)
            {
                AddFunction(module, qbeType, function);
            }
        }
    }

    private List<Action> _functionBodies = new();

    private void AddFunction(QbeModule module, QbeType? qbeType, FunctionDeclarationExpression function)
    {
        // This. This is the big one.
        var newName = CodeGenHelpers.QbeGetCustomFunctionName(qbeType, function.Identifier);
        var qbeFunction = module.AddFunction(newName, function.Export ? QbeFunctionFlags.Export : QbeFunctionFlags.None, GetTypeDefinition((IHasType)function.ReturnType), function.IsVariadic, GetQbeArguments(function.Arguments));
        if (qbeType != null)
        {
            if (!_functions.TryGetValue(qbeType, out var functionList))
            {
                functionList = new List<QbeFunction>();
                _functions[qbeType] = functionList;
            }
        }
        else
        {
            _globalFunctions.Add(qbeFunction);
        }
        
        // Define the actual function body.
        _functionBodies.Add(() =>
        {
            if (function.Expressions.Count > 0)
            {
                var start = qbeFunction.BuildEntryBlock();
                _expressionConverter.CurrentBlock = start;
                _blockStack.Push((start, new Dictionary<string, IQbeRef>()));
                foreach (var expr in function.Expressions)
                {
                    AddExpression(expr);
                }

                _blockStack.Pop();
            }
        });
        // Do nothing if there are no expressions.
    }

    private Stack<(QbeBlock block, Dictionary<string,IQbeRef> variables)> _blockStack = new();

    private void AddExpression(Expression expr)
    {
        _expressionConverter.ConvertExpressionToQbe(expr);
    }

    private QbeArgument[]? GetQbeArguments(List<VariableDeclarationExpression> functionArguments)
    {
        if (functionArguments.Count == 0)
            return null;

        QbeArgument[] qbeArguments = new QbeArgument[functionArguments.Count];
        for (int i = 0; i < functionArguments.Count; i++)
        {
            var arg = functionArguments[i];
            qbeArguments[i] = new QbeArgument(GetTypeDefinition(arg.Type), arg.Identifier);
        }
        return qbeArguments;
    }

    private void AddTypes(QbeModule module, List<AnalyzerNamespace> analyzerNamespaces)
    {
        Dictionary<string, AnalyzerType> analyzerDefinedTypes = new();
        foreach (var ns in analyzerNamespaces)
        {
            var prefix = ns.GetFullName();
            foreach (var type in ns.Types)
            {
                analyzerDefinedTypes[$"{prefix}.{type.Name}"] = type;
            }
        }
        foreach (var type in analyzerDefinedTypes)
        {
            AddType(module, type);
        }
    }    
    
    private void AddTypes(QbeModule module, Dictionary<string, AnalyzerType> analyzerDefinedTypes)
    {
        foreach (var type in analyzerDefinedTypes)
        {
            AddType(module, type);
        }
    }

    private void AddType(QbeModule module, KeyValuePair<string, AnalyzerType> type)
    {
        var newName = CodeGenHelpers.QbeGetCustomTypeName(type.Key);
        var qbeType = module.AddType(newName);
        _types[type.Key] = (type.Value, qbeType);
    }

    private void AddField(QbeModule module, QbeType qbeType, ClassMemberDeclaration field)
    {
        if (field.TypeLiteral != Literal.Custom)
        {
            var type = CodeGenHelpers.QbeGetLiteralType(field.TypeLiteral);
            qbeType.Add(type);
            return;
        }

        var name = ((IFlattenable)field.TypeExpression).Flatten();
        if (_types.TryGetValue(name, out var value))
        {
            // Check if the 
            qbeType.AddRef(value.Item2);
        }
    }
    
    private IQbeTypeDefinition? GetTypeDefinition(IHasType typeExpression)
    {
        if (typeExpression.TypeLiteral == Literal.Void)
        {
            return null;
        }
        
        if (typeExpression.TypeLiteral != Literal.Custom)
        {
            return CodeGenHelpers.QbeGetLiteralType(typeExpression.TypeLiteral);
        }

        var name = ((IFlattenable)typeExpression).Flatten();
        if (_types.TryGetValue(name, out var value))
        {
            return value.Item2;
        }

        throw new ArgumentException($"Type {name} not found in defined types.");
    }

    private void AddStrings(QbeModule module, Dictionary<LiteralExpression, string> strings)
    {
        foreach (var literal in strings)
        {
            AddString(module, literal);
        }
    }

    private void AddString(QbeModule module, KeyValuePair<LiteralExpression, string> literal)
    {
        var reference = module.AddGlobal(literal.Value);
        _stringLiterals[literal.Key] = reference;
    }
}