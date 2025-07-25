using System.Globalization;
using QbeGenerator;
using QuickGraph.Collections;
using RumLang.Analyzer;
using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.CodeGen;

public class ExpressionConverter(Stack<(QbeBlock block, Dictionary<string,(IQbeRef, IQbeTypeDefinition?)> variables)> _blockStack,
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
                if (!((QbeValue)arg).PrimitiveEnum.Equals(expected.Primitive))
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
            
            case UnaryExpression unaryExpression:
                return ConvertUnaryExpression(unaryExpression, targetType);
            
            case ThisExpression thisExpression:
                return ConvertThisExpression(thisExpression, targetType);
            
            case NewExpression newExpression:
                return ConvertNewExpression(newExpression);
            
            case MemberAccessExpression memberAccess:
                return ConvertMemberAccessExpression(memberAccess);
            
            default:
                throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported.");
        }
    }

    private IQbeRef ConvertMemberAccessExpression(MemberAccessExpression memberAccess)
    {
        // Otherwise, we need to evaluate the target first.
        var target = EvaluateExpression(memberAccess.Target);
        if (target is not QbeValue targetValue || target is not QbeLocalRef localRef)
        {
            throw new InvalidOperationException($"Member access target must be a QbeValue, but got {target.GetType().Name}.");
        }
        
        QbeType typeDefinition;
        // Find the target in the current scope
        var targetTuple = _blockStack.Peek().variables.FirstOrDefault(x => x.Value.Item1.Equals(targetValue));
        typeDefinition = (QbeType)targetTuple.Value.Item2!;
        
        // Now we can get the AnalyzerType from the type definition.
        var typeInfo = _types.FirstOrDefault(x => x.Value.Item2.Equals(typeDefinition));
        if (typeInfo.Equals(default(KeyValuePair<string, (AnalyzerType, QbeType)>)))
        {
            throw new KeyNotFoundException($"Type \"{typeDefinition.Identifier}\" not found in the types dictionary.");
        }
        var typeDef = typeInfo.Value.Item1;
        var idx = typeDef.Members.FindIndex(m => m.Identifier == memberAccess.MemberName);
        var targetType = typeDefinition.GetDefinition().Definitions[idx].Primitive;
        if (targetType == null)
        {
            // Set it to a pointer
            targetType = QbePrimitive.Pointer;
        }
        
        // This is the opposite of storing, we need to load the member from the target.
        // To do this, we will use the offset of the member in the type definition.
        var loadOp = CurrentBlock!.LoadFromType(targetType, targetValue, typeDefinition, idx, is32Bit);
        return loadOp;
    }

    private IQbeRef ConvertNewExpression(NewExpression newExpression)
    {
        var targetType = newExpression.Type;
        var args = newExpression.Arguments;
        
        // Get the qbeType
        var flattenedType = ((IFlattenable)targetType).Flatten();
        if (!_types.TryGetValue(flattenedType, out var typeInfo))
        {
            throw new KeyNotFoundException($"Type \"{flattenedType}\" not found.");
        }
        
        var qbeType = typeInfo.Item2;
        
        // Allocate it. 
        var size = qbeType.ByteSize(is32Bit);
        var allocated = CurrentBlock!.Allocate(size); // This is what we will be returning, but first we check if there is a constructor for this type.
        
        var constructorName = CodeGenHelpers.QbeGetCustomFunctionName(qbeType, qbeType.Identifier);
        if (_functions.TryGetValue(qbeType, out var functions) && functions.Any(f => f.Identifier == constructorName))
        {
            var constructor = functions.First(f => f.Identifier == constructorName);
            var argsRefs = args.Select(arg => EvaluateExpression(arg)).ToArray();
            
            // Insert the allocated pointer as the first argument to the constructor.
            if (argsRefs.Length == 0 || !(argsRefs[0] is QbeValue firstArg) || firstArg.PrimitiveEnum != QbePrimitive.Pointer)
            {
                // If the first argument is not a pointer, we need to add it.
                argsRefs = new[] { allocated }.Concat(argsRefs).ToArray();
            }
            else
            {
                // If the first argument is already a pointer, we can just use it.
                argsRefs[0] = allocated;
            }
            
            CurrentBlock!.Call(constructor.Identifier, null, argsRefs.Cast<QbeValue>().ToArray());
        }
        
        // No constructor found, we just return the allocated pointer.
        return allocated;
    }

    private IQbeRef ConvertThisExpression(ThisExpression thisExpression, IQbeTypeDefinition? targetType)
    {
        // Just return the current function's argument called "this", if it does not exist, throw an exception.
        if (CurrentBlock == null)
        {
            throw new InvalidOperationException("Current block is not set. Cannot convert 'this' expression.");
        }

        var type = thisExpression.TypeName;
        if (!_types.TryGetValue(type, out var typeInfo))
        {
            throw new KeyNotFoundException($"Type \"{type}\" not found.");
        }

        var thisVariable = typeInfo.Item1.Functions.FirstOrDefault(f => f.Identifier == thisExpression.FunctionName); 
        if (thisVariable == null)
        {
            throw new KeyNotFoundException($"Function 'this' not found in type {type}.");
        }

        return Qbe.LRef(typeInfo.Item2, "this");
    }

    private IQbeRef ConvertUnaryExpression(UnaryExpression unaryExpression, IQbeTypeDefinition? targetType)
    {
        var value = EvaluateExpression(unaryExpression.Value, targetType);
        if (value is not QbeValue qbeValue)
        {
            throw new InvalidOperationException($"Unary operation on non-QbeValue type {value.GetType().Name} is not supported.");
        }

        switch (unaryExpression.Operator)
        {
            case Operator.Plus:
                return qbeValue; // Unary plus does nothing.
            case Operator.Minus:
                return CurrentBlock!.Negate(qbeValue);
            case Operator.Asterisk:
                if (qbeValue.PrimitiveEnum is QbePrimitive primitive && primitive.PrimitiveEnum == QbePrimitiveEnum.Pointer)
                {
                    return CurrentBlock!.Load(primitive, value);
                }
                throw new InvalidOperationException("Dereferencing only works with pointers.");
            default:
                throw new NotSupportedException($"Unary operation {unaryExpression.Operator} is not supported.");
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
        if (!GetVariable(identifier.Identifier, out var variable, out _))
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
            if (!SetVariable(identifier, ptr, qbeType))
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
            if (!GetVariable(variableName, out _, out _))
                throw new KeyNotFoundException($"Variable \"{variableName}\" not found.");
        }
        else if (assignment.Lhs is VariableDeclarationExpression decl)
        {
            ConvertVariableDeclarationExpression(decl);
            variableName = decl.Identifier;
            if (!GetVariable(variableName, out _, out _))
                throw new KeyNotFoundException($"Variable \"{variableName}\" not found after declaration.");
        }
        else if (assignment.Lhs is UnaryExpression unary)
        {
            // Check if it is a dereference operation
            if (unary.Operator == Operator.Asterisk && unary.Value is IdentifierExpression idExpr)
            {
                // Special case, we need to dereference the identifier. We can NOT use the identifier directly.
                variableName = idExpr.Identifier;
                
                if (!GetVariable(variableName, out var variable, out _))
                    throw new KeyNotFoundException($"Variable \"{variableName}\" not found for dereferencing.");
                if (variable is not QbeLocalRef localRef)
                {
                    throw new InvalidOperationException(
                        $"Variable \"{variableName}\" is not a local reference and cannot be dereferenced.");
                }
                
                // We need to dereference the variable, so we will use the pointer to the variable.
                if (localRef.PrimitiveEnum is not QbePrimitive primitive)
                {
                    throw new InvalidOperationException(
                        $"Variable \"{variableName}\" is not a pointer and cannot be dereferenced.");
                }

                if (primitive.PrimitiveEnum != QbePrimitiveEnum.Pointer)
                {
                    throw new InvalidOperationException(
                        $"Variable \"{variableName}\" is not a pointer type, cannot dereference.");
                }
                
                // evaluate the right-hand side expression.
                var toStore = EvaluateExpression(assignment.Rhs, primitive);
                if (toStore is not QbeValue valueToStore)
                {
                    throw new InvalidOperationException(
                        $"Cannot assign value of type {toStore.GetType().Name} to variable \"{variableName}\".");
                }
                
                CurrentBlock!.Store((QbeValue)variable, valueToStore);
                return; // We are done with the dereference assignment.
            }
            else if (unary.Operator == Operator.Asterisk && unary.Value is UnaryExpression subUnary)
            {
                throw new NotSupportedException(
                    $"Dereferencing a unary expression is not supported in assignment. Expression: {subUnary.GetStringRepresentation()}");
            }
            else
            {
                throw new NotSupportedException($"Unary operation {unary.Operator} is not supported in assignment.");
            }
        }
        else if (assignment.Lhs is MemberAccessExpression memberAccess)
        {
            variableName = memberAccess.MemberName;
            var variable = GetVariable(variableName, out var existingVar, out _);
            var target = EvaluateExpression(memberAccess.Target, ((QbeValue)existingVar).PrimitiveEnum);
            if (target is not QbeValue targetValue)
            {
                throw new InvalidOperationException(
                    $"Member access target must be a QbeValue, but got {target.GetType().Name}.");
            }
            
            // use the store operator to store the value in the member. Using the StoreToType method in CurrentBlock.
            // IQbeTypeDefinition type, QbeValue prt, QbeValue value, QbeType typeDefinition, int idx, bool is32Bit is the signature.
            (AnalyzerType, QbeType) typeInfo;
            
            if (memberAccess.Target is IdentifierExpression identifierExpr)
            {
                if (!GetVariable(identifierExpr.Identifier, out var typeReference, out var definition))
                {
                    throw new KeyNotFoundException($"Variable \"{identifierExpr.Identifier}\" not found.");
                }

                var a = _types.FirstOrDefault(x => x.Value.Item2.Equals(definition!));
                typeInfo = a.Value;
            }
            else if (memberAccess.Target is ThisExpression thisExpr)
            {
                // This is a special case, we need to get the type from the current function.
                if (!_types.TryGetValue(thisExpr.TypeName, out typeInfo))
                {
                    throw new KeyNotFoundException($"Type \"{thisExpr.TypeName}\" not found.");
                }
            }
            else if (!_types.TryGetValue(((ThisExpression)memberAccess.TypeExpression).TypeName, out typeInfo))
            {
                throw new KeyNotFoundException($"Type \"{((IFlattenable)memberAccess.Target).Flatten()}\" not found.");
            }
            var typeDefinition = typeInfo.Item2;
            var member = typeInfo.Item1.Members.FirstOrDefault(m => m.Identifier == variableName);
            if (member == null)
            {
                throw new KeyNotFoundException($"Member \"{variableName}\" not found in type {typeDefinition.Identifier}.");
            }
            var idx = typeInfo.Item1.Members.IndexOf(member);
            if (idx < 0)
            {
                throw new KeyNotFoundException($"Member \"{variableName}\" not found in type {typeDefinition.Identifier}.");
            }

            var rhs = EvaluateExpression(assignment.Rhs, typeDefinition);
            CurrentBlock!.StoreToType(typeDefinition, targetValue, (QbeValue)rhs, typeDefinition, idx, is32Bit);
        }
        else
        {
            throw new NotSupportedException($"Assignment to {assignment.Lhs.GetType().Name} is not supported.");
        }

        var result = EvaluateExpression(assignment.Rhs,
            GetVariable(variableName, out var existingVariable, out _) ? ((QbeValue)existingVariable).PrimitiveEnum : null);
        //var copied = CurrentBlock!.Copy((QbeValue)result);

        if (!SetVariable(variableName, result))
            throw new KeyNotFoundException($"Variable \"{variableName}\" not found for assignment.");
    }
    
    public bool GetVariable(string identifier, out IQbeRef variable, out IQbeTypeDefinition? typeDefinition)
    {
        // Check the current block\"s variables first
        typeDefinition = null;
        if (_blockStack.Peek().variables.TryGetValue(identifier, out var localVariable))
        {
            variable = localVariable.Item1;
            typeDefinition = localVariable.Item2;
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
                typeDefinition = function.Arguments.First(x => x.Identifier == identifier).Primitive;
                return true;
            }
        }

        // If still not found, check the types
        foreach (var type in _types.Keys)
        {
            if (_types[type].Item1.Members.Any(x => x.Identifier == identifier))
            {
                var member = _types[type].Item1.Members.First(x => x.Identifier == identifier);
                // Now that we have the member, we need its type
                var memberType = _types[type].Item2.GetDefinition().Definitions[_types[type].Item1.Members.IndexOf(member)];
                IQbeTypeDefinition qbeType = QbePrimitive.Pointer;
                if (memberType.Primitive != null)
                    qbeType = memberType.Primitive;
                variable = new QbeLocalRef(qbeType, member.Identifier);
                typeDefinition = _types[type].Item2;
                
                return true;
            }
        }

        // If not found anywhere, return false
        variable = null!;
        typeDefinition = null;
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

    public bool SetVariable(string identifier, IQbeRef variable, IQbeTypeDefinition? OverrideTypeDefinition = null)
    {
        IQbeTypeDefinition typeDefinition = OverrideTypeDefinition ?? ((QbeValue)variable).PrimitiveEnum;
        if (_blockStack.Count == 0)
        {
            throw new InvalidOperationException("Block stack is empty. Cannot set variable.");
        }
        if (_blockStack.Peek().variables.ContainsKey(identifier))
        {
            _blockStack.Peek().variables[identifier] = (variable, _blockStack.Peek().variables[identifier].Item2 ?? typeDefinition);
            return true;
        }
        // If the variable is not found in the current block, we can try to add it.
        _blockStack.Peek().variables.Add(identifier, (variable, typeDefinition));
        return true;
    }

    public QbeBlock? CurrentBlock { get; set; } = _currentBlock;
}