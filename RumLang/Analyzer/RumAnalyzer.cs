using System.Collections;
using System.Diagnostics;
using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Analyzer;

public enum AnalyzerResultType
{
    Debug,
    Success,
    Error,
    Warning
}

public record AnalyzerResult(AnalyzerResultType Type, string Message);

public class RumAnalyzer
{
    private readonly List<AnalyzerResult> _results = new();
    private readonly List<AstNode> _nodes;
    private readonly Rum _rum;
    private long _milliseconds = 0;

    private string _entryPoint = string.Empty;
    
    public IReadOnlyList<AnalyzerResult> Results => _results;
    public AnalyzerOptions Options { get; set; } = new AnalyzerOptions();
    public List<DependencyEntry> ExternalDependencies => 
        _externalNamespaceMap.Values.SelectMany(x => x).ToList();

    private readonly Dictionary<string, List<DependencyEntry>> _externalNamespaceMap = new();
    private readonly Dictionary<string, FunctionDeclarationExpression> _externalFunctionMap = new();
    private readonly Dictionary<string, AnalyzerType> _externalTypeMap = new();

    private readonly Dictionary<string, List<AnalyzerNamespace>> _definedNamespaces = new();
    private readonly Dictionary<string, FunctionDeclarationExpression> _definedFunctions = new();
    private readonly Dictionary<string, AnalyzerType> _definedTypes = new();
    
    private readonly Dictionary<LiteralExpression, string> _stringLiterals = new();
    public Dictionary<LiteralExpression, string> StringLiterals => _stringLiterals;
    private readonly List<AstNode> _externalAstNodes = new();
    private readonly List<string> _includedCHeaders = new();
    public List<string> IncludedCHeaders => _includedCHeaders;
    
    // Join the 2 dictionaries for external types and defined types
    public Dictionary<string, AnalyzerType> DefinedTypes => _definedTypes;
    public Dictionary<string, FunctionDeclarationExpression> DefinedFunctions => _definedFunctions;
    public string? EntryPoint => _entryPoint;

    public RumAnalyzer(Rum rum, List<AstNode> nodes)
    {
        _rum = rum;
        _nodes = nodes;

        var sw = Stopwatch.StartNew();
        ScanDependencies();
        sw.Stop();
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success,
            $"Dependency scan completed in {sw.ElapsedMilliseconds} milliseconds. Found {_externalNamespaceMap.Count} unique namespaces."));

        sw.Restart();
        CrawlDependencies();
        sw.Stop();
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success,
            $"Crawling dependencies completed in {sw.ElapsedMilliseconds} milliseconds. Found {_externalFunctionMap.Count} functions and {_externalTypeMap.Count} types."));
    }

    public List<AnalyzerResult> Analyze()
    {
        var stopwatch = Stopwatch.StartNew();

        var externalEntry = new ExternalEntry(_nodes, Options);
        ProcessNamespaces(externalEntry.Namespaces);
        ProcessFileLocals(externalEntry);
        ProcessStringLiterals(_nodes);
        
        // Now that we have everything defined, we can actually crawl the code and analyze it.
        var nodeAnalyzer = new NodeAnalyzer(_rum, _definedNamespaces, _externalNamespaceMap, _definedFunctions, _definedTypes,
            _externalFunctionMap, _externalTypeMap, _results, Options);
        
        nodeAnalyzer.AnalyzeNodes(_nodes, ref _entryPoint);
        
        // Process external nodes
        nodeAnalyzer.AnalyzeNodes(_externalAstNodes, ref _entryPoint);
        ProcessStringLiterals(_externalAstNodes);
        
        stopwatch.Stop();
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success,
            $"Analysis finished in {stopwatch.ElapsedMilliseconds} milliseconds."));

        return _results;
    }

    private void ProcessStringLiterals(List<AstNode> nodes, int depth = 0)
    {
        if (depth > 10) // Prevent deep recursion
            return;
        foreach (var node in nodes)
        {
            if (node is LiteralExpression literal)
            {
                if (literal.TypeLiteral == Literal.String)
                {
                    // Process string literal
                    var value = literal.Value;
                    if (value != null && !string.IsNullOrWhiteSpace(value))
                    {
                        if (_stringLiterals.ContainsKey(literal))
                        {
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Warning,
                                $"Duplicate string literal found: \"{value}\" at line {literal.LineNumber}, column {literal.ColumnNumber}. Ignoring duplicate."));
                        }
                        else
                        {
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Debug,
                                $"Found string literal: \"{value}\" at line {literal.LineNumber}, column {literal.ColumnNumber}."));

                            _stringLiterals[literal] = value;
                        }
                    }
                }
            }

            // Recursively process child nodes
            if (node is IHasChildren hasChildren)
            {
                ProcessStringLiterals(hasChildren.GetChildren().SelectMany(x => x).ToList(), depth + 1);
            }
        }
    }

    private void ScanDependencies()
    {
        foreach (var dir in _rum.LibraryDirectories.Where(Directory.Exists))
        {
            foreach (var file in Directory.GetFiles(dir, "*.rum", SearchOption.AllDirectories))
            {
                var entry = new DependencyEntry(_rum, file, Options);
                _externalAstNodes.AddRange(entry.ExternalEntry.Nodes);
                foreach (var ns in entry.ExternalEntry.Namespaces)
                {
                    var fullName = ns.GetFullName();
                    if (!_externalNamespaceMap.TryGetValue(fullName, out var entries))
                    {
                        entries = new List<DependencyEntry>();
                        _externalNamespaceMap[fullName] = entries;
                    }
                    entries.Add(entry);
                    _results.Add(new AnalyzerResult(AnalyzerResultType.Success,
                        $"Namespace \"{fullName}\" defined in file: {file}"));
                }
            }
        }
    }

    private void CrawlDependencies()
    {
        var visitedNamespaces = new HashSet<string>();

        foreach (var (nsName, entries) in _externalNamespaceMap)
        {
            if (visitedNamespaces.Contains(nsName))
                continue;

            visitedNamespaces.Add(nsName);

            foreach (var entry in entries)
            {
                foreach (var ns in entry.ExternalEntry.Namespaces)
                {
                    if (ns.GetFullName() != nsName)
                        continue;

                    var baseName = ns.GetFullName();

                    foreach (var func in ns.Functions)
                        AddToMap(_externalFunctionMap, $"{baseName}.{func.Identifier}", func, "Function");

                    foreach (var type in ns.Types)
                        AddToMap(_externalTypeMap, $"{baseName}.{type.Name}", type, "Type");
                }
            }
        }
    }

    private void ProcessNamespaces(List<AnalyzerNamespace> namespaces, AnalyzerNamespace? parent = null)
    {
        foreach (var ns in namespaces)
        {
            var fullName = ns.GetFullName();
            _definedNamespaces.TryAdd(fullName, new List<AnalyzerNamespace>());
            _definedNamespaces[fullName].Add(ns);

            foreach (var type in ns.Types)
                AddToMap(_definedTypes, $"{fullName}.{type.Name}", type, "Type");

            foreach (var func in ns.Functions)
                AddToMap(_definedFunctions, $"{fullName}.{func.Identifier}", func, "Function");

            _results.Add(new AnalyzerResult(AnalyzerResultType.Success,
                $"Namespace \"{fullName}\" defined."));

            ProcessNamespaces(ns.SubNamespaces, ns);
        }
    }

    private void ProcessFileLocals(ExternalEntry entry)
    {
        foreach (var type in entry.TopLevelTypes)
            AddToMap(_definedTypes, type.Name, type, "File-local type");

        foreach (var func in entry.TopLevelFunctions)
            AddToMap(_definedFunctions, func.Identifier, func, "File-local function");
    }

    private void AddToMap<T>(Dictionary<string, T> map, string key, T value, string label)
    {
        if (!map.ContainsKey(key))
        {
            map[key] = value;
            _results.Add(new AnalyzerResult(AnalyzerResultType.Success, $"{label} \"{key}\" defined."));
        }
        else
        {
            _results.Add(new AnalyzerResult(AnalyzerResultType.Warning,
                $"{label} \"{key}\" is already defined. Ignoring duplicate definition."));
        }
    }
}
