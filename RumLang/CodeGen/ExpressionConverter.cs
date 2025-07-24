using QbeGenerator;
using RumLang.Analyzer;
using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.CodeGen;

public class ExpressionConverter(Stack<(QbeBlock block, Dictionary<string,IQbeRef> variables)> _blockStack,
    QbeBlock? _currentBlock,
    Dictionary<LiteralExpression, QbeGlobalRef> _stringLiterals,
    Dictionary<string, (AnalyzerType, QbeType)> _types, 
    Dictionary<QbeType, List<QbeFunction>> _functions, 
    List<QbeFunction> _globalFunctions, 
    bool is32Bit = false)
{
    public void ConvertExpressionToQbe(Expression expr)
    {
        if (CurrentBlock == null)
        {
            throw new InvalidOperationException("Current block is not set. Cannot convert expression.");
        }
        
        switch (expr)
        {
            case AssignmentExpression assignment:
                ConvertAssignmentExpression(assignment);
                break;
            case VariableDeclarationExpression variableDeclaration:
                ConvertVariableDeclarationExpression(variableDeclaration);
                break;
            case ReturnExpression returnExpresion:
                ConvertReturnExpression(returnExpresion);
                break;
            default:
                // Handle other expression types here
                // For example, if you have a LiteralExpression, IdentifierExpression, etc.
                // You would convert them to QBE representations as needed.
                throw new NotImplementedException($"Conversion for {expr.GetType().Name} is not implemented.");
                break;
        }
    }

    private void ConvertReturnExpression(ReturnExpression returnExpresion)
    {
        var expression = returnExpresion.Value;
        if (expression == null)
        {
            CurrentBlock!.Return();
        }
        else if (expression is LiteralExpression literal)
        {
            var qbeType = CodeGenHelpers.QbeGetLiteralType(literal.TypeLiteral);
            if (qbeType.IsFloat())
            {
                double parsedValue = double.Parse(literal.Value);
                var literalValue = new QbeLiteral(qbeType, parsedValue);
                CurrentBlock!.Return(CurrentBlock!.Copy(literalValue));
            }
            else if (qbeType.IsInteger())
            {
                long parsedValue = long.Parse(literal.Value);
                var literalValue = new QbeLiteral(qbeType, parsedValue);
                CurrentBlock!.Return(CurrentBlock!.Copy(literalValue));
            }
            else
            {
                throw new NotSupportedException($"Literal type {qbeType} is not supported for return.");
            }
        }
        else if (expression is IdentifierExpression identifier)
        {
            // Check if the identifier exists in the current block\"s variables, or in the global scope
            if (_blockStack.Peek().variables.TryGetValue(identifier.Identifier, out var variable))
            {
                CurrentBlock!.Return((QbeValue)variable);
            }
            else
            {
                // If the variable does not exist, we can throw an error or handle it accordingly
                throw new KeyNotFoundException($"Variable \"{identifier.Identifier}\" not found in current block.");
            }
        }
        else
        {
            throw new NotSupportedException($"Return of type {expression.GetType().Name} is not supported in this context.");
        }
    }

    private void ConvertVariableDeclarationExpression(VariableDeclarationExpression variableDeclaration)
    {
        var identifier = variableDeclaration.Identifier;
        var type = variableDeclaration.Type;

        // Convert the type to a QBE type
        IQbeTypeDefinition qbeType = null;
        if (type.TypeLiteral != Literal.Custom)
        {
            qbeType = CodeGenHelpers.QbeGetLiteralType(type.TypeLiteral);

            // As for literals we can just initialize them as 0, later we will load a value into them
            if (qbeType.IsFloat())
            {
                var variable = CurrentBlock!.Copy(new QbeLiteral(qbeType, 0.0));
                if (!SetVariable(identifier, variable))
                {
                    throw new KeyNotFoundException($"Variable \"{identifier}\" not found in current block.");
                }
            }
            else if (qbeType.IsInteger())
            {
                var variable = CurrentBlock!.Copy(new QbeLiteral(qbeType, 0));
                if (!SetVariable(identifier, variable))
                {
                    throw new KeyNotFoundException($"Variable \"{identifier}\" not found in current block.");
                }
            }
        }
        else
        {
            string flattenedType = ((IFlattenable)type).Flatten();
            qbeType = _types[flattenedType].Item2;
            
            // Create an alloc instruction for the custom type
            var ptr = CurrentBlock!.Allocate(qbeType.ByteSize(is32Bit));
            SetVariable(identifier, ptr);
        }
    }

    private void ConvertAssignmentExpression(AssignmentExpression assignment)
    {
        var lhs = assignment.Lhs;
        var rhs = assignment.Rhs;

        IQbeRef variable;
        string variableName;
        // Check if the lhs is an identifier, or a variable declaration
        if (lhs is IdentifierExpression identifier)
        {
            var identifierName = identifier.Identifier;
            if (GetVariable(identifierName, out variable))
            {
                variableName = identifierName;
            }
            else
            {
                // If the variable does not exist, we need to create it
                throw new KeyNotFoundException($"Variable \"{identifierName}\" not found in current block.");
            }
        }
        else if (lhs is VariableDeclarationExpression variableDeclaration)
        {
            ConvertVariableDeclarationExpression(variableDeclaration);
            var identifierName = variableDeclaration.Identifier;
            if (GetVariable(identifierName, out variable))
            {
                variableName = identifierName;
            }
            else
            {
                throw new KeyNotFoundException($"Variable \"{identifierName}\" not found in current block.");
            }
        }
        else
        {
            throw new NotSupportedException($"Assignment to {lhs.GetType().Name} is not supported.");
        }
        
        if (rhs is LiteralExpression literal)
        {
            var qbeType = CodeGenHelpers.QbeGetLiteralType(literal.TypeLiteral);

            if (qbeType.IsFloat())
            {
                double parsedValue = double.Parse(literal.Value);
                var literalValue = new QbeLiteral(qbeType, parsedValue);
                // Use the copy instruction, then overwrite teh variable reference
                variable = CurrentBlock!.Copy(literalValue);
            }
            else if (qbeType.IsInteger())
            {
                long parsedValue = long.Parse(literal.Value);
                var literalValue = new QbeLiteral(qbeType, parsedValue);
                // Use the copy instruction, then overwrite the variable reference
                variable = CurrentBlock!.Copy(literalValue);
            }
            else
            {
                throw new NotSupportedException($"Literal type {qbeType} is not supported for assignment.");
            }
        }
        else
            throw new NotSupportedException(
                $"Assignment of type {rhs.GetType().Name} is not supported in this context.");
        
        // Update the variables dictionary with the new variable reference
        if (!SetVariable(variableName, variable))
        {
            throw new KeyNotFoundException($"Variable \"{variableName}\" not found in current block.");
        }
    }
    
    public bool GetVariable(string identifier, out IQbeRef variable)
    {
        // Check the current block\"s variables first
        if (_blockStack.Peek().variables.TryGetValue(identifier, out variable))
        {
            return true;
        }

        // If not found, check the global functions
        foreach (var function in _globalFunctions)
        {
            if (function.Arguments.Any(x => x.Identifier == identifier))
            {
                variable = new QbeLocalRef(
                    function.Arguments.First(x => x.Identifier == identifier).Primitive,
                    function.Arguments.First(x => x.Identifier == identifier).Identifier);
                return true;
            }
        }

        // If still not found, check the types
        foreach (var type in _types.Keys)
        {
            if (_types[type].Item1.Members.Any(x => x.Identifier == identifier))
            {
                throw new NotImplementedException(
                    $"Accessing type members in expressions is not implemented yet. Type: {type}, Identifier: {identifier}");
                return true;
            }
        }

        // If not found anywhere, return false
        variable = null!;
        return false;
    }
    
    public bool SetVariable(string identifier, IQbeRef variable)
    {
        // Check the current block's variables first
        if (_blockStack.Peek().variables.ContainsKey(identifier))
        {
            _blockStack.Peek().variables[identifier] = variable;
            return true;
        }

        // If not found, check the global functions
        foreach (var function in _globalFunctions)
        {
            if (function.Arguments.Any(x => x.Identifier == identifier))
            {
                variable = new QbeLocalRef(
                    function.Arguments.First(x => x.Identifier == identifier).Primitive,
                    function.Arguments.First(x => x.Identifier == identifier).Identifier);
                return true;
            }
        }

        // If still not found, check the types
        foreach (var type in _types.Keys)
        {
            if (_types[type].Item1.Members.Any(x => x.Identifier == identifier))
            {
                throw new NotImplementedException(
                    $"Accessing type members in expressions is not implemented yet. Type: {type}, Identifier: {identifier}");
            }
        }

        // If not found anywhere, add it to the current block's variables
        if (_blockStack.Count > 0)
        {
            _blockStack.Peek().variables[identifier] = variable;
            return true;
        }
        return false;
    }

    public QbeBlock? CurrentBlock { get; set; } = _currentBlock;
}