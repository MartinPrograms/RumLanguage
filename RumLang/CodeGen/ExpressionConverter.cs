using System.Globalization;
using QbeGenerator;
using QuickGraph.Collections;
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
    AnalyzerOptions options,
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
            case FunctionCallExpression functionCall:
                ConvertFunctionCallExpression(functionCall);
                break;
            default:
                // Handle other expression types here
                // For example, if you have a LiteralExpression, IdentifierExpression, etc.
                // You would convert them to QBE representations as needed.
                throw new NotImplementedException($"Conversion for {expr.GetType().Name} is not implemented.");
                break;
        }
    }

    private IQbeRef? ConvertFunctionCallExpression(FunctionCallExpression functionCall, IQbeTypeDefinition? lhsType = null)
    {
        var name = ((IFlattenable)functionCall.FunctionTarget).Flatten();
        var function = GetFunction(name);
        if (function == null)
        {
            throw new KeyNotFoundException($"Function \"{name}\" not found.");
        }
        
        var type = GetFunctionReturnType(name,out bool isCfunction, lhsType);
        var refs = functionCall.Arguments.Select(x => EvaluateExpression(x)).ToList();
        var temp = new List<IQbeRef>();

        if (!isCfunction)
        {
            // For each argument, we need to ensure it matches the expected type.
            var expectedArgs = _globalFunctions.FirstOrDefault(f => f.Identifier == function)?.Arguments;
            if (expectedArgs == null)
            {
                throw new KeyNotFoundException($"Function \"{function}\" not found in global functions.");
            }
            
            if (expectedArgs.Count != refs.Count())
            {
                throw new ArgumentException($"Function \"{function}\" expects {expectedArgs.Count} arguments, but got {refs.Count()}.");
            }
            
            foreach (var (arg, expected) in refs.Zip(expectedArgs, (r, e) => (r, e)))
            {
                QbeValue qbeArg = (QbeValue)arg;
                if (((QbeValue)arg).PrimitiveEnum != expected.Primitive)
                {
                    // Try to implicitly cast the argument to the expected type
                    var casted = AnalyzerType.ImplicitCast(((QbeValue)arg).PrimitiveEnum, expected.Primitive);
                    if (casted != expected.Primitive)
                    {
                        throw new InvalidOperationException(
                            $"Cannot implicitly cast {((QbeValue)arg).PrimitiveEnum} to {expected.Primitive} for function {function}.");
                    }
                    qbeArg.PrimitiveEnum = expected.Primitive;
                }
                
                temp.Add((qbeArg));
            }
            
            refs = temp;
        }
        
        var args = refs.ToArray().Select(x => ((QbeValue)x)).ToArray();

        return CurrentBlock!.Call(function, type, args);
    }
    
    private IQbeRef EvaluateExpression(Expression expr, IQbeTypeDefinition? targetType = null)
    {
        switch (expr)
        {
            case LiteralExpression literal:
                return CreateLiteral(literal, targetType);

            case IdentifierExpression identifier:
                return GetVariable(identifier);

            case BinaryExpression binary:
                return ConvertBinaryExpression(binary);
            
            case FunctionCallExpression functionCall:
                return ConvertFunctionCallExpression(functionCall, targetType)!; 

            // Extend this with function calls, casting, logical ops, etc.

            default:
                throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported.");
        }
    }

    private IQbeRef ConvertBinaryExpression(BinaryExpression binary)
    {
        var left = (QbeValue)EvaluateExpression(binary.Lhs);
        var right = (QbeValue)EvaluateExpression(binary.Rhs, left.PrimitiveEnum); // For C function calls, we will assume the type is correct. :p

        var type = left.PrimitiveEnum;
        if (type is QbePrimitive primitive)
        {
            if (!primitive.Equals(right.PrimitiveEnum))
            {
                TryImplicitCast(left, right);
            }

            var result = binary.Operator switch
            {
                Operator.Plus => CurrentBlock!.Add(left, right),
                Operator.Minus => CurrentBlock!.Sub(left, right),
                Operator.Asterisk => CurrentBlock!.Mul(left, right),
                Operator.Divide => CurrentBlock!.Div(left, right),
                Operator.Modulus => CurrentBlock!.Rem(left, right),
                _ => throw new NotSupportedException(
                    $"Binary operation {binary.Operator} for type {type} is not supported in this context.")
            };
            
            return result;
        }
        else
        {
            throw new NotImplementedException(
                $"Binary operation for custom types is not implemented yet.");
        }
    }

    private static void TryImplicitCast(QbeValue left, QbeValue right)
    {
        bool canImplicitlyCast = AnalyzerType.ImplicitCast(left.PrimitiveEnum, right.PrimitiveEnum)
            .Equals(right.PrimitiveEnum) && right is QbeLiteral;
        if (!canImplicitlyCast)
            throw new InvalidOperationException(
                $"Cannot implicitly cast {left.PrimitiveEnum} to {right.PrimitiveEnum} in binary operation.");
                
        // If we can implicitly cast, we will do so.
        right.PrimitiveEnum = left.PrimitiveEnum;
    }

    private IQbeRef GetVariable(IdentifierExpression identifier)
    {
        if (!GetVariable(identifier.Identifier, out var variable))
            throw new KeyNotFoundException($"Variable \"{identifier.Identifier}\" not found.");
        return (QbeValue)variable;
    }

    private IQbeRef CreateLiteral(LiteralExpression literal, IQbeTypeDefinition? targetType = null)
    {
        if (literal.TypeLiteral == Literal.String)
        {
            return _stringLiterals.TryGetValue(literal, out var stringRef)
                ? stringRef
                : throw new KeyNotFoundException($"String literal \"{literal.Value}\" not found.");
        }
        
        var qbeType = CodeGenHelpers.QbeGetLiteralType(literal.TypeLiteral);
        
        // Check if qbeType matches the target type, if not try a cast
        if (targetType != null && !qbeType.Equals(targetType))
        {
            qbeType = AnalyzerType.ImplicitCast(qbeType, targetType);
        }
        
        if (qbeType.IsFloat())
        {
            return new QbeLiteral(qbeType, double.Parse(literal.Value, CultureInfo.InvariantCulture));
        }
        else if (qbeType.IsInteger())
        {
            return new QbeLiteral(qbeType, long.Parse(literal.Value, CultureInfo.InvariantCulture));
        }
        
        throw new NotSupportedException($"Literal type {literal.TypeLiteral} is not supported.");
    }

    private void ConvertReturnExpression(ReturnExpression returnExpression)
    {
        if (returnExpression.Value == null)
        {
            CurrentBlock!.Return();
        }
        else
        {
            var value = EvaluateExpression(returnExpression.Value);
            CurrentBlock!.Return((QbeValue)value);
        }
    }
    
    private void ConvertVariableDeclarationExpression(VariableDeclarationExpression variableDeclaration)
    {
        var identifier = variableDeclaration.Identifier;
        var type = variableDeclaration.Type;
        IQbeTypeDefinition qbeType;

        if (type.TypeLiteral != Literal.Custom)
        {
            qbeType = CodeGenHelpers.QbeGetLiteralType(type.TypeLiteral);
            var initValue = qbeType.IsFloat()
                ? new QbeLiteral(qbeType, 0.0)
                : new QbeLiteral(qbeType, 0);
            var variable = CurrentBlock!.Copy(initValue);
            if (!SetVariable(identifier, variable))
                throw new KeyNotFoundException($"Failed to declare variable \"{identifier}\".");
        }
        else
        {
            var flattened = ((IFlattenable)type).Flatten();
            qbeType = _types[flattened].Item2;
            var ptr = CurrentBlock!.Allocate(qbeType.ByteSize(is32Bit));
            if (!SetVariable(identifier, ptr))
            {
                throw new KeyNotFoundException($"Failed to declare variable \"{identifier}\" of type {flattened}.");
            }
        }
    }

    private void ConvertAssignmentExpression(AssignmentExpression assignment)
    {
        string variableName;

        if (assignment.Lhs is IdentifierExpression identifier)
        {
            variableName = identifier.Identifier;
            if (!GetVariable(variableName, out _))
                throw new KeyNotFoundException($"Variable \"{variableName}\" not found.");
        }
        else if (assignment.Lhs is VariableDeclarationExpression decl)
        {
            ConvertVariableDeclarationExpression(decl);
            variableName = decl.Identifier;
            if (!GetVariable(variableName, out _))
                throw new KeyNotFoundException($"Variable \"{variableName}\" not found after declaration.");
        }
        else
        {
            throw new NotSupportedException($"Assignment to {assignment.Lhs.GetType().Name} is not supported.");
        }

        var result = EvaluateExpression(assignment.Rhs,
            GetVariable(variableName, out var existingVariable) ? ((QbeValue)existingVariable).PrimitiveEnum : null);
        var copied = CurrentBlock!.Copy((QbeValue)result);

        if (!SetVariable(variableName, copied))
            throw new KeyNotFoundException($"Variable \"{variableName}\" not found for assignment.");
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
    
    private string GetFunction(string name)
    {
        // Check if the function is a C function
        if (name.StartsWith(options.CPrefix))
        {
            // assume whatever, probably valid or something, remove everything before the last identifier
            var lastDotIndex = name.LastIndexOf('.');
            if (lastDotIndex != -1)
            {
                name = name.Substring(lastDotIndex + 1);
            }

            return name;
        }
        
        // Check if the function is a global function
        foreach (var function in _globalFunctions)
        {
            if (function.Identifier == name)
            {
                return function.Identifier;
            }
        }
        
        // Check if the function is defined in the types
        foreach (var type in _types.Keys)
        {
            if (_types[type].Item1.Functions.Any(x => x.Identifier == name))
            {
                return CodeGenHelpers.QbeGetCustomFunctionName(_types[type].Item2, name);
            }
        }
        
        throw new KeyNotFoundException($"Function \"{name}\" not found.");
    }
    
    private IQbeTypeDefinition? GetFunctionReturnType(string name, out bool isCfunction, IQbeTypeDefinition? lhsType = null)
    {
        isCfunction = false;
        // Check if the function is a C function
        if (name.StartsWith(options.CPrefix))
        {
            // assume whatever, probably valid or something, remove everything before the last identifier
            var lastDotIndex = name.LastIndexOf('.');
            if (lastDotIndex != -1)
            {
                name = name.Substring(lastDotIndex + 1);
            }
            isCfunction = true;

            return lhsType ?? null;
        }
        
        // Check if the function is a global function
        foreach (var function in _globalFunctions)
        {
            if (function.Identifier == name)
            {
                return function.ReturnType;
            }
        }
        
        // Check if the function is defined in the types
        foreach (var type in _types.Keys)
        {
            if (_types[type].Item1.Functions.Any(x => x.Identifier == name))
            {
                var ihastype = _types[type].Item1.Functions.First(x => x.Identifier == name).ReturnType;
                if (ihastype.TypeLiteral != Literal.Custom)
                {
                    return CodeGenHelpers.QbeGetLiteralType(ihastype.TypeLiteral);
                }
                else
                {
                    return _types[type].Item2;
                }
            }
        }
        
        throw new KeyNotFoundException($"Function \"{name}\" not found.");
    }

    public bool SetVariable(string identifier, IQbeRef variable)
    {
        if (_blockStack.Count == 0)
        {
            throw new InvalidOperationException("Block stack is empty. Cannot set variable.");
        }
        if (_blockStack.Peek().variables.ContainsKey(identifier))
        {
            _blockStack.Peek().variables[identifier] = variable;
            return true;
        }
        // If the variable is not found in the current block, we can try to add it.
        _blockStack.Peek().variables.Add(identifier, variable);
        return true;
    }

    public QbeBlock? CurrentBlock { get; set; } = _currentBlock;
}