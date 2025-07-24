using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Analyzer;

public class NodeAnalyzer(Rum rum, Dictionary<string, List<AnalyzerNamespace>> definedNamespaces,Dictionary<string, List<DependencyEntry>> externalNamespaceMap, Dictionary<string, FunctionDeclarationExpression> definedFunctions, Dictionary<string, AnalyzerType> definedTypes, Dictionary<string, FunctionDeclarationExpression> externalFunctionMap, Dictionary<string, AnalyzerType> externalTypeMap, List<AnalyzerResult> results, AnalyzerOptions options)
{
    private Stack<List<VariableDeclarationExpression>> _variableStack = new(); // To keep track of scope.
    private ClassDeclarationExpression _currentType;
    private FunctionDeclarationExpression _currentFunction;
    private readonly Dictionary<LiteralExpression, string> _literalStrings = new(); // To collect all defined strings.
    
    private string entryPoint = string.Empty; // To keep track of the entry point function.
    public void AnalyzeNode(AstNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node), "Node cannot be null.");

        switch (node)
        {
            case ImportExpression import:
                AnalyzeImport(import);
                break;
            case FunctionDeclarationExpression function:
                AnalyzeFunction(function);
                break;
            case ClassDeclarationExpression classDecl:
                AnalyzeClass(classDecl);
                break;
            case VariableDeclarationExpression variable:
                AnalyzeVariable(variable);
                break;
            case IdentifierExpression identifier:
                AnalyzeIdentifier(identifier);
                break;
            case AssignmentExpression assignment:
                AnalyzeAssignment(assignment);
                break;
            case ClassMemberDeclaration member:
                AnalyzeClassMember(member);
                break;
            case FunctionCallExpression functionCall:
                AnalyzeFunctionCall(functionCall); // This also checks if argument types match.
                break;
            case LiteralExpression literal:
                AnalyzeLiteral(literal);
                break;
            case ReturnExpression returnExpr:
                AnalyzeReturn(returnExpr);
                break;
            case LiteralTypeExpression type:
                AnalyzeLiteralType(type);
                break;
            case BinaryExpression binary:
                AnalyzeBinaryExpression(binary);
                break;
            case MemberAccessExpression memberAccess:
                AnalyzeMemberAccess(memberAccess);
                break;
            case NewExpression newExpr:
                AnalyzeNewExpression(newExpr);
                break;
            case NamespaceDeclarationExpression:
                AnalyzeNamespace(node as NamespaceDeclarationExpression);
                break;
            default:
                results.Add(new AnalyzerResult(AnalyzerResultType.Warning, $"Unhandled node type: {node.GetType().Name} at line {node.LineNumber}, column {node.ColumnNumber}."));
                break;
        }
    }

    private void AnalyzeNamespace(NamespaceDeclarationExpression? node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node), "Namespace declaration cannot be null.");
        
        // Analyze it's contents.
        if (node.Nodes != null && node.Nodes.Count > 0)
        {
            foreach (var child in node.Nodes)
            {
                AnalyzeNode(child);
            }
        }
    }

    private void AnalyzeNewExpression(NewExpression newExpr)
    {
        if (newExpr == null)
            throw new ArgumentNullException(nameof(newExpr), "New expression cannot be null.");

        // Check if the type is defined.
        if (newExpr.Type.TypeLiteral == Literal.Custom)
        {
            string typeName = "<unknown>";
            if (newExpr.Type is IFlattenable flattenable)
            {
                typeName = flattenable.Flatten();
            }
            else
            {
                typeName = newExpr.Type.TypeExpression is IFlattenable f ? f.Flatten() : "<unknown>";
            }
            
            if (!definedTypes.ContainsKey(typeName) && !externalTypeMap.ContainsKey(typeName) && !typeName.StartsWith(options.CPrefix))
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Custom type \"{typeName}\" is not defined at line {newExpr.LineNumber}, column {newExpr.ColumnNumber}."));
            }
            
            // We can assume the type is valid, now we can do type checking for the constructor arguments.
            if (newExpr.Arguments.Count > 0)
            {
                var targetTypeString = newExpr.Type is IFlattenable flattenableType ? flattenableType.Flatten() : "<unknown>";
                var targetType = definedTypes.ContainsKey(targetTypeString)
                    ? definedTypes[targetTypeString]
                    : externalTypeMap.ContainsKey(targetTypeString)
                        ? externalTypeMap[targetTypeString]
                        : null;
                if (targetType == null)
                {
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Custom type \"{typeName}\" is not defined at line {newExpr.LineNumber}, column {newExpr.ColumnNumber}."));
                    return;
                }

                var constructor = targetType.Functions.FirstOrDefault(f => f.Identifier == targetType.Name); // The constructor is just a function with the same name as the type.
                if (constructor == null)
                {
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Custom type \"{typeName}\" has no constructor defined, but arguments were still passed! Line {newExpr.LineNumber}, column {newExpr.ColumnNumber}."));
                    return;
                }
                
                bool isVariadic = constructor.IsVariadic;
                int expectedArgCount = constructor.Arguments.Count;
                if (newExpr.Arguments.Count != expectedArgCount && !isVariadic)
                {
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Constructor for type \"{typeName}\" expects {expectedArgCount} arguments but got {newExpr.Arguments.Count} at line {newExpr.LineNumber}, column {newExpr.ColumnNumber}."));
                    return;
                }
                if (newExpr.Arguments.Count < expectedArgCount && !isVariadic)
                {
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Constructor for type \"{typeName}\" expects at least {expectedArgCount} arguments but got {newExpr.Arguments.Count} at line {newExpr.LineNumber}, column {newExpr.ColumnNumber}."));
                    return;
                }
                
                // Now we can do some type checking for the arguments.
                for (int i = 0; i < constructor.Arguments.Count; i++)
                {
                    var thisArg = newExpr.Arguments[i];
                    var targetArg = constructor.Arguments[i];

                    if (thisArg is IHasType hasTypeArg && targetArg is IHasType hasTypeTargetArg)
                    {
                        // String exemption. Strings are converted to custom type.
                        var customType = options.DefaultStringClass;
                        if (hasTypeTargetArg.TypeExpression is IFlattenable flattenable2)
                        {
                            // Check if hasTypeArg is a string and targetArg is a custom type where flattenable.Flatten() matches the custom type.
                            var flattenedType = flattenable2.Flatten();
                            if (flattenedType == customType && hasTypeArg.TypeLiteral == Literal.String)
                            {
                                // This is a valid string argument.
                                continue;
                            }
                        }
                        
                        // Check if the target is a pointer, if so we can pass in any type.
                        if (hasTypeTargetArg.TypeLiteral == Literal.Pointer)
                        {
                            // This is a valid pointer argument, we can pass in any type.
                            continue;
                        }
                        
                        // Check if the argument can be implicitly converted (think of long to int, or double to float).
                        var implicitCastType =
                            AnalyzerType.ImplicitCast(hasTypeArg.TypeLiteral, hasTypeTargetArg.TypeLiteral);
                        if (implicitCastType != hasTypeTargetArg.TypeLiteral)
                        {
                            // If the implicit cast type does not match the target argument type, add an error.
                            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Argument {i + 1} of constructor for type \"{typeName}\" expects type {hasTypeTargetArg.TypeLiteral} but got {hasTypeArg.TypeLiteral} at line {thisArg.LineNumber}, column {thisArg.ColumnNumber}."));
                            continue;
                        }
                    }
                }
            }
        }
        else
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"New expression must have a custom type at line {newExpr.LineNumber}, column {newExpr.ColumnNumber}. Got {newExpr.Type.GetType().Name} instead."));
        }
    }

    private void AnalyzeMemberAccess(MemberAccessExpression memberAccess)
    {
        if (memberAccess == null)
            throw new ArgumentNullException(nameof(memberAccess), "Member access expression cannot be null.");

        // Flatten the member access to an identifier.
        var flattenedIdentifier = new IdentifierExpression(memberAccess.Flatten(), memberAccess.LineNumber, memberAccess.ColumnNumber);
        AnalyzeIdentifier(flattenedIdentifier);
    }

    private void AnalyzeBinaryExpression(BinaryExpression binary)
    {
        if (binary == null)
            throw new ArgumentNullException(nameof(binary), "Binary expression cannot be null.");

        // Analyze the left and right expressions.
        AnalyzeNode(binary.Lhs);
        AnalyzeNode(binary.Rhs);
        
        // TODO: Check if the operator is valid in this case. (Wont do this for now, as it is not a priority.)
        
        // Check the types of the left and right expressions.
        if (binary.Lhs is IHasType lhsType && binary.Rhs is IHasType rhsType)
        {
            // If both sides have types, we can check if they are compatible.
            if (lhsType.TypeLiteral != rhsType.TypeLiteral)
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Binary operation between {lhsType.TypeLiteral} and {rhsType.TypeLiteral} is not allowed at line {binary.LineNumber}, column {binary.ColumnNumber}."));
            }

            if (lhsType.TypeLiteral == Literal.Custom && rhsType.TypeLiteral == Literal.Custom)
            {
                var lhsFlattened = lhsType.TypeExpression is IFlattenable lhsFlattenable ? lhsFlattenable.Flatten() : "<unknown>";
                var rhsFlattened = rhsType.TypeExpression is IFlattenable rhsFlattenable ? rhsFlattenable.Flatten() : "<unknown>";
                if (lhsFlattened != rhsFlattened)
                {
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Binary operation between custom types {lhsFlattened} and {rhsFlattened} is not allowed at line {binary.LineNumber}, column {binary.ColumnNumber}."));
                }
            }
        }
        else
        {
            // Check if x, or y is an identifier, if so, we can assume it is a variable.
            if (binary.Lhs is IdentifierExpression lhsIdentifier)
            {
                AnalyzeIdentifier(lhsIdentifier);
            }
            if (binary.Rhs is IdentifierExpression rhsIdentifier)
            {
                AnalyzeIdentifier(rhsIdentifier);
            }
            
            // TODO: We should do type checking on these but whateverrrr 
        }
    }

    private void AnalyzeLiteralType(LiteralTypeExpression type)
    {
        // Dont do anything, this is just a type expression. Like "int", "float", "string", etc.
    }

    private void AnalyzeReturn(ReturnExpression returnExpr)
    {
        if (returnExpr == null)
            throw new ArgumentNullException(nameof(returnExpr), "Return expression cannot be null.");

        // Check if we are inside a function.
        if (_currentType == null && _currentFunction == null)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Return statement outside of a function at line {returnExpr.LineNumber}, column {returnExpr.ColumnNumber}."));
            return;
        }

        // If the return expression is not null, analyze it.
        if (returnExpr.Value != null)
        {
            AnalyzeNode(returnExpr.Value!);
        }
    }

    private void AnalyzeLiteral(LiteralExpression literal)
    {
        if (literal == null)
            throw new ArgumentNullException(nameof(literal), "Literal expression cannot be null.");
    }

    private void AnalyzeFunctionCall(FunctionCallExpression functionCall)
    {
        if (functionCall == null)
            throw new ArgumentNullException(nameof(functionCall), "Function call expression cannot be null.");
        
        // Check if the function is defined in the current scope.
        if (functionCall.FunctionTarget is IdentifierExpression identifier)
        {
            if (!definedFunctions.ContainsKey(identifier.Identifier) && !externalFunctionMap.ContainsKey(identifier.Identifier) && 
                (_currentType == null || _currentType.Functions.All(f => f.Identifier != identifier.Identifier)))
            {
                if (identifier.Identifier.StartsWith(options.CPrefix))
                    return; // C functions are allowed to be called without being defined.
                // If the function is not defined, add an error.
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function \"{identifier.Identifier}\" is not defined at line {identifier.LineNumber}, column {identifier.ColumnNumber}."));
                return;
            }
        }
        else if (functionCall.FunctionTarget is MemberAccessExpression access)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Debug, $"Flattening member access ({access.Flatten()}) to identifier."));
            identifier = new IdentifierExpression(access.Flatten(), access.LineNumber, access.ColumnNumber);
        }
        else
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function call target must be an identifier at line {functionCall.LineNumber}, column {functionCall.ColumnNumber}. Got {functionCall.FunctionTarget.GetType().Name} instead."));
            return;
        }

        var targetFunction = definedFunctions.ContainsKey(identifier.Identifier)
            ?
            definedFunctions[identifier.Identifier]
            :
            externalFunctionMap.ContainsKey(identifier.Identifier)
                ? externalFunctionMap[identifier.Identifier]
                :
                _currentType?.Functions.FirstOrDefault(f => f.Identifier == identifier.Identifier);

        if (targetFunction == null)
        {
            if (identifier.Identifier.StartsWith(options.CPrefix))
                return; // C functions are allowed to be called without being defined.

            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function \"{identifier.Identifier}\" is not defined at line {identifier.LineNumber}, column {identifier.ColumnNumber}."));
            return;
        }
        
        var isVariadic = targetFunction.IsVariadic;
        var expectedArgCount = targetFunction.Arguments.Count;
        if (functionCall.Arguments.Count != expectedArgCount && !isVariadic)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function \"{identifier.Identifier}\" expects {expectedArgCount} arguments but got {functionCall.Arguments.Count} at line {functionCall.LineNumber}, column {functionCall.ColumnNumber}."));
            return;
        }
        
        if (functionCall.Arguments.Count < expectedArgCount && !isVariadic)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function \"{identifier.Identifier}\" expects at least {expectedArgCount} arguments but got {functionCall.Arguments.Count} at line {functionCall.LineNumber}, column {functionCall.ColumnNumber}."));
            return;
        }
        
        // Now we can do some type checking for the arguments.
        for (int i = 0; i < targetFunction.Arguments.Count; i++)
        {
            var thisArg = functionCall.Arguments[i];
            var targetArg = targetFunction.Arguments[i];

            if (thisArg is IHasType hasTypeArg && targetArg is IHasType hasTypeTargetArg)
            {
                // String exemption. Strings are converted to custom type.
                var customType = options.DefaultStringClass;
                if (hasTypeTargetArg.TypeExpression is IFlattenable flattenable)
                {
                    // Check if hasTypeArg is a string and targetArg is a custom type where flattenable.Flatten() matches the custom type.
                    var flattenedType = flattenable.Flatten();
                    if (flattenedType == customType && hasTypeArg.TypeLiteral == Literal.String)
                    {
                        // This is a valid string argument.
                        continue;
                    }
                }
                
                // Check if the argument can be implicitly converted (think of long to int, or double to float).
                var implicitCastType = AnalyzerType.ImplicitCast(hasTypeArg.TypeLiteral, hasTypeTargetArg.TypeLiteral);
                if (implicitCastType != hasTypeTargetArg.TypeLiteral)
                {
                    // If the implicit cast type does not match the target argument type, add an error.
                    results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Argument {i + 1} of function \"{identifier.Identifier}\" expects type {hasTypeTargetArg.TypeLiteral} but got {hasTypeArg.TypeLiteral} at line {thisArg.LineNumber}, column {thisArg.ColumnNumber}."));
                    continue;
                }
                
                // Convert the current node to the implicit cast type if it was the reason why it passed
                if (implicitCastType != hasTypeArg.TypeLiteral)
                {
                    // This is a valid implicit cast, we can continue.
                    results.Add(new AnalyzerResult(AnalyzerResultType.Debug, $"Implicitly casting argument {i + 1} of function \"{identifier.Identifier}\" from {hasTypeArg.TypeLiteral} to {implicitCastType} at line {thisArg.LineNumber}, column {thisArg.ColumnNumber}."));
                }
            }
        }
    }

    private void AnalyzeClassMember(ClassMemberDeclaration member)
    {
        if (member == null)
            throw new ArgumentNullException(nameof(member), "Class member declaration cannot be null.");
        
        // Make sure its already defined, if not error.
        if (_currentType == null)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Class member \"{member.Identifier}\" defined outside of a class at line {member.LineNumber}, column {member.ColumnNumber}."));
            return;
        }
    }

    private void AnalyzeAssignment(AssignmentExpression assignment)
    {
        if (assignment == null)
            throw new ArgumentNullException(nameof(assignment), "Assignment expression cannot be null.");

        // Analyze the left-hand side identifier.
        if (assignment.Lhs is IdentifierExpression identifier)
        {
            AnalyzeIdentifier(identifier);
        }
        else if (assignment.Lhs is VariableDeclarationExpression variable)
        {
            AnalyzeVariable(variable);
        }
        else if (assignment.Lhs is MemberAccessExpression access)
        {
            string identifierName = access.MemberName;
            if (identifierName == options.ThisKeyword && 
                (_currentType == null || _currentType.ClassName != options.ThisKeyword))
            {
                // 'this' keyword is valid, no action needed.
                return;
            }
            
            if (identifierName == options.ThisKeyword && _currentType == null)
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Cannot use 'this' keyword outside of a class at line {access.LineNumber}, column {access.ColumnNumber}."));
            }
            
            // Flatten the member access to an identifier.
            var flattenedIdentifier = new IdentifierExpression(access.Flatten(), access.LineNumber, access.ColumnNumber);
            AnalyzeIdentifier(flattenedIdentifier);
        }
        else
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Left-hand side of assignment must be an identifier at line {assignment.LineNumber}, column {assignment.ColumnNumber}. Got {assignment.Lhs.GetType().Name} instead."));
        }
        
        // Analyze the right-hand side expression.
        AnalyzeNode(assignment.Rhs);
    }

    private void AnalyzeIdentifier(IdentifierExpression identifier)
    {
        // First check if the identifier is defined in the current scope.
        if (identifier == null)
            throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null.");
        
        if (AnalyzerType.BuiltInTypes.ContainsKey(identifier.Identifier))
        {
            return; // Identifier is a built-in type, no action needed.
        }

        bool isVariable = _variableStack.Count > 0 &&
                          _variableStack.Peek().Any(v => v.Identifier == identifier.Identifier);
        if (_currentFunction != null)
        {
            if (_currentFunction.Arguments.Any(arg => arg.Identifier == identifier.Identifier))
            {
                isVariable = true; // Identifier is an argument of the current function.
            }
        }
        if (isVariable)
        {
            return; // Identifier is defined in the current scope, no action needed.
        }
        
        bool isType = definedTypes.ContainsKey(identifier.Identifier) || externalTypeMap.ContainsKey(identifier.Identifier);
        if (isType)
        {
            return; // Identifier is a type, no action needed.
        }
        
        bool isFunction = definedFunctions.ContainsKey(identifier.Identifier) || externalFunctionMap.ContainsKey(identifier.Identifier);
        if (isFunction)
        {
            return; // Identifier is a function, no action needed.
        }
        
        // Check if the identifier is defined in the member properties of the current class.
        string id = identifier.Identifier;
        if (identifier.Identifier.StartsWith(Keyword.This.ToString().ToLower() + "."))
        {
            id = id.Substring(Keyword.This.ToString().Length + 1); // Remove 'this.' prefix.
        }
        if (_currentType != null && _currentType.Variables.Any(v => v.Identifier == id))
        {
            return; // Identifier is a member variable of the current class, no action needed.
        }
        
        if (_currentType != null && _currentType.Functions.Any(f => f.Identifier == id))
        {
            return; // Identifier is a member function of the current class, no action needed.
        }
        
        results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Identifier \"{identifier.Identifier}\" is not defined in the current scope at line {identifier.LineNumber}, column {identifier.ColumnNumber}."));
    }

    private void AnalyzeVariable(VariableDeclarationExpression variable)
    {
        if (variable == null)
            throw new ArgumentNullException(nameof(variable), "Variable declaration cannot be null.");

        // Check if the variable is already defined in the current scope.
        if (_variableStack.Count > 0 && _variableStack.Peek().Any(v => v.Identifier == variable.Identifier))
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Variable \"{variable.Identifier}\" is already defined in the current scope at line {variable.LineNumber}, column {variable.ColumnNumber}."));
            return;
        }
        
        // Check if the variable type is defined.
        var variableType = variable.TypeExpression;
        if (variableType == null)
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Variable \"{variable.Identifier}\" has no type defined at line {variable.LineNumber}, column {variable.ColumnNumber}."));
            return;
        }

        if (variableType is IHasType hasType && hasType.TypeLiteral == Literal.Custom)
        {
            string typeName = "<unknown>";
            if (variableType is IFlattenable f)
            {
                typeName = f.Flatten();
            }
            else
            {
                typeName = hasType.TypeExpression is IFlattenable flattenable
                    ? flattenable.Flatten()
                    : "<unknown>";
            }
            
            if (!definedTypes.ContainsKey(typeName) && !externalTypeMap.ContainsKey(typeName) && !typeName.StartsWith(options.CPrefix))
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Custom type \"{typeName}\" is not defined at line {variable.LineNumber}, column {variable.ColumnNumber}."));
            }
        }
        else if (variableType is LiteralTypeExpression typeExpression)
        {
            // Do nothing, we know this is valid.
        }
        else
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Variable type must be an identifier or a custom type at line {variable.LineNumber}, column {variable.ColumnNumber}. Got {variableType.GetType().Name} instead."));
        }

        // Add the variable to the current scope.
        if (_variableStack.Count == 0)
        {
            _variableStack.Push(new List<VariableDeclarationExpression>());
        }
        
        _variableStack.Peek().Add(variable);
    }

    private void AnalyzeClass(ClassDeclarationExpression classDecl)
    {
        var oldType = _currentType;
        _currentType = classDecl;
        if (classDecl == null)
            throw new ArgumentNullException(nameof(classDecl), "Class declaration cannot be null.");
        
        if (classDecl is IHasChildren hasChildren)
        {
            var children = hasChildren.GetChildren();
            if (children != null)
            {
                foreach (var group in children)
                {
                    AnalyzeNodes(group, ref entryPoint);
                }
            }
        }

        _currentType = oldType;
    }

    private void AnalyzeFunction(FunctionDeclarationExpression function)
    {
        if (function == null)
            throw new ArgumentNullException(nameof(function), "Function declaration cannot be null.");
        
        // Push a new scope for the function.
        _variableStack.Push(new List<VariableDeclarationExpression>());
        _currentFunction = function;
        
        if (function.IsEntryPoint)
        {
            if (!string.IsNullOrWhiteSpace(entryPoint))
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Multiple entry points defined at line {function.LineNumber}, column {function.ColumnNumber}. Previous entry point was at line {entryPoint}"));
            }
            else
            {
                entryPoint = $"{function.Identifier}";
            }
        }
        
        // Analyze the function arguments.
        foreach (var arg in function.Arguments)
        {
            if (arg is VariableDeclarationExpression variable)
            {
                AnalyzeVariable(variable);
            }
            else
            {
                results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Function argument must be a variable declaration at line {arg.LineNumber}, column {arg.ColumnNumber}."));
            }
        }
        
        // Analyze the function block
        if (function is IHasChildren hasChildren)
        {
            var children = hasChildren.GetChildren();
            if (children != null)
            {
                foreach (var group in children)
                {
                    AnalyzeNodes(group, ref entryPoint);
                }
            }
        }
        
        // Pop the function scope.
        _variableStack.Pop();
        _currentFunction = null;
        
        string isConstructor = function.Identifier == _currentType?.ClassName ? " (constructor)" : string.Empty;
        results.Add(new AnalyzerResult(AnalyzerResultType.Success, $"Function \"{function.Identifier}\" defined{isConstructor}"));
    }

    private void AnalyzeImport(ImportExpression import)
    {
        // Check if the import is defined. If not add an error.
        if (!definedNamespaces.ContainsKey(import.Target) && !externalNamespaceMap.ContainsKey(import.Target) && !import.Target.StartsWith(options.CPrefix))
        {
            results.Add(new AnalyzerResult(AnalyzerResultType.Error, $"Undefined import: {import.Target} at line {import.LineNumber}, column {import.ColumnNumber}."));
            return;
        }
    }

    public void AnalyzeNodes(List<AstNode> nodes, ref string entryPoint)
    {
        _variableStack.Push(new List<VariableDeclarationExpression>());
        foreach (var node in nodes)
        {
            AnalyzeNode(node);
        }
        _variableStack.Pop(); // Pop the scope after analyzing all nodes.

        entryPoint = this.entryPoint;
    }
}