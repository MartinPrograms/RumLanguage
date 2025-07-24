using System.Diagnostics;
using System.Text;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Parser;

public enum ParserError
{
    Success,
    InvalidArgument
}

public record ParserResult(ParserError Error, List<AstNode>? Root, string? ErrorString);

public class RumParser : IDebugInfo
{
    private readonly List<Token> _tokens;
    private List<AstNode> _nodes;
    private int _position = 0;
    private long _milliseconds = 0;

    private bool _isInFunction = false;
    private bool _isInClass = false;

    private Stack<(string Namespace, List<AstNode> Nodes)> _namespaces = new();
    private List<AstNode> _currentNodes;
    
    public static readonly Dictionary<Operator, (float LeftPrecedence, float RightPrecedence)> PrecedenceMap = new()
    {
        [Operator.Assignment] = (1, 1),
        [Operator.Or] = (2, 3),
        [Operator.And] = (4, 5),
        [Operator.Equal] = (6, 7),
        [Operator.NotEqual] = (6, 7),
        [Operator.LessThan] = (8, 9),
        [Operator.LessThanOrEqual] = (8, 9),
        [Operator.GreaterThan] = (8, 9),
        [Operator.GreaterThanOrEqual] = (8, 9),
        [Operator.BitwiseOr] = (10, 11),
        [Operator.BitwiseXor] = (12, 13),
        [Operator.BitwiseAnd] = (14, 15),
        [Operator.LeftShift] = (16, 17),
        [Operator.RightShift] = (16, 17),
        [Operator.Plus] = (18, 19),
        [Operator.Minus] = (18, 19),
        [Operator.Asterisk] = (20, 21),
        [Operator.Divide] = (20, 21),
        [Operator.Modulus] = (20, 21),
        [Operator.PointerAccess] = (30, 31),
        [Operator.MemberAccess] = (30, 31)
    };

    public RumParser(List<Token> tokens)
    {
        _tokens = tokens;
        _nodes = new List<AstNode>();
        _currentNodes = _nodes;
    }
    
    public ParserResult Parse()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            while (!IsAtEnd())
            {
                if (Check(TokenType.Comment) || Check(TokenType.Whitespace))
                {
                    Advance();
                    continue;
                }

                if (Match(Punctuation.RightBrace))
                {
                    if (_namespaces.Count <= 0)
                    {
                        throw new Exception($"Unexpected closing brace! {Previous()}");
                    }

                    _namespaces.Pop();
                    if (_namespaces.Count > 0)
                        _currentNodes = _namespaces.Peek().Nodes;
                    else
                        _currentNodes = _nodes; // Move back to the global nodes

                    continue;
                }

                if (Match(Keyword.Import))
                {
                    if (_isInFunction || _namespaces.Count > 0)
                    {
                        throw new Exception("Can not import within function scope.");
                    }

                    StringBuilder sb = new();
                    while (!Match(Punctuation.Semicolon))
                    {
                        if (Peek()!.Type == TokenType.Identifier)
                        {
                            sb.Append(Peek()!.Value);
                        }
                        else
                        {
                            sb.Append(".");
                        }

                        Advance();
                    }

                    _nodes.Add(new ImportExpression(sb.ToString(), GetLineNumber(), GetColumnNumber()));

                    continue;
                }

                if (IsNamespaceStart())
                {
                    if (_isInFunction || _isInClass)
                    {
                        throw new Exception("Can not declare namespace within function or class scope.");
                    }
                    var hasAccessModifier = Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Public ||
                                             Peek()!.Keyword == Keyword.Private || Peek()!.Keyword == Keyword.Internal;
                    AccessModifier accessModifier = AccessModifier.Public; // Default to public
                    if (hasAccessModifier)
                    {
                        if (Peek()!.Keyword == Keyword.Private)
                            accessModifier = AccessModifier.Private;
                        if (Peek()!.Keyword == Keyword.Internal)
                            accessModifier = AccessModifier.Internal;

                        Advance(); // Consume the access modifier
                    }
                    Advance(); // Consume the "namespace" keyword
                    
                    var namespaceName = Peek()!.Value;
                    var nodes = new List<AstNode>();
                    _currentNodes.Add(new NamespaceDeclarationExpression(namespaceName, nodes, GetLineNumber(), GetColumnNumber(), accessModifier));
                    _currentNodes = nodes;
                    _namespaces.Push((namespaceName, nodes));
                    Advance();
                    if (!Match(Punctuation.LeftBrace))
                        throw new Exception("Expected \"{\" after namespace identifier!");
                    
                    continue;
                }

                if (IsClassStart())
                {
                    if (_isInClass || _isInFunction)
                    {
                        throw new Exception("Can not declare class within function or class scope.");
                    }
                    _isInClass = true;
                    var accessModifier = AccessModifier.Private; // Default to private
                    if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Private ||
                        Peek()!.Keyword == Keyword.Internal || Peek()!.Keyword == Keyword.Public)
                    {
                        if (Peek()!.Keyword == Keyword.Internal)
                            accessModifier = AccessModifier.Internal;
                        if (Peek()!.Keyword == Keyword.Public)
                            accessModifier = AccessModifier.Public;

                        Advance();
                    }

                    Advance(); // Consume "class" 
                    var className = Peek()!.Value;
                    Advance(); // Consume the class name
                    if (!Match(Punctuation.LeftBrace))
                        throw new Exception($"Expected \"{{\" after class name! At {Peek()}");
                    
                    // Now we are in a class scope, we can add class members.
                    List<FunctionDeclarationExpression> functions = new();
                    List<ClassMemberDeclaration> variables = new();
                    FunctionDeclarationExpression? constructor = null; // Will be set later if found, can be found by having no return type. `public/internal/private constructor()`

                    while (!Match(Punctuation.RightBrace))
                    {
                        bool hasAccessModifier =
                            Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Private ||
                            Peek()!.Keyword == Keyword.Internal || Peek()!.Keyword == Keyword.Public;
                        AccessModifier memberAccessModifier = AccessModifier.Private; // Default to private
                        if (hasAccessModifier)
                        {
                            if (Peek()!.Keyword == Keyword.Internal)
                                memberAccessModifier = AccessModifier.Internal;
                            if (Peek()!.Keyword == Keyword.Public)
                                memberAccessModifier = AccessModifier.Public;

                            Advance();
                        }

                        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Export)
                            throw new Exception(
                                "The \"export\" keyword is only allowed on top-level or namespace-scoped functions. Class member functions cannot be exported directly.");
                        var type = GetMemberedType();

                        // Check if it is a constructor by checking if the next token is a left parenthesis.
                        if (Peek()!.Value == className &&
                            Peek(1)!.Type == TokenType.Punctuation &&
                            Peek(1)!.Punctuation == Punctuation.LeftParenthesis)
                        {
                            // This *is* a constructor!
                            Advance();
                            Advance(); // We are now in argument space.

                            bool isVariadic = false;
                            var arguments = GetArguments(ref isVariadic);
                            if (!Match(Punctuation.LeftBrace))
                                throw new Exception("Expected \"{\" after constructor definition!");

                            var codeblock = ParseCodeBlock();
                            constructor = new FunctionDeclarationExpression(className, arguments,
                                type,
                                accessModifier, codeblock, isVariadic,GetLineNumber(), GetColumnNumber());
                            continue;
                        }

                        var name = Peek(1)!.Value;
                        if (type is not IHasType)
                            throw new Exception($"Expected a type for member \"{name}\" at {GetLineNumber()}:{GetColumnNumber()}");
                        Advance();
                        Advance();
                        bool isFunction = Peek()!.Type == TokenType.Punctuation &&
                                          Peek()!.Punctuation == Punctuation.LeftParenthesis;
                        bool isVariable = Peek()!.Type == TokenType.Punctuation &&
                                          Peek()!.Punctuation == Punctuation.Semicolon;

                        if (!isFunction && !isVariable)
                            throw new Exception($"Expected punctuation, \"(\" or \";\" after declaration at {Peek()}");

                        if (isVariable)
                        {
                            variables.Add(new ClassMemberDeclaration(name, (IHasType)type, memberAccessModifier, GetLineNumber(), GetColumnNumber()));
                            Advance();
                            continue;
                        }

                        if (isFunction)
                        {
                            Advance(); // Now in argument space.
                            var isVariadic = false;
                            var arguments = GetArguments(ref isVariadic);
                            if (Match(Punctuation.Semicolon))
                            {
                                _currentNodes.Add(new FunctionDeclarationExpression(name, arguments, type,
                                    accessModifier,
                                    new(), isVariadic, GetLineNumber(), GetColumnNumber(), false));
                                continue;
                            }

                            Advance(); // Consume the {
                            var expressions = ParseCodeBlock(); // Also consumes the }
                            functions.Add(new FunctionDeclarationExpression(name, arguments, type,
                                memberAccessModifier, expressions, isVariadic, GetLineNumber(), GetColumnNumber(), false));
                            continue;

                        }
                    }

                    // Now we can create the class itself
                    if (constructor != null)
                        functions.Add(constructor);
                    _currentNodes.Add(new ClassDeclarationExpression(className, accessModifier, functions, variables, GetLineNumber(), GetColumnNumber()));
                    _isInClass = false;
                    continue;
                }

                if (IsFunctionStart())
                {
                    // Now consume the function!
                    bool hasAccessModifier = Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Private ||
                                             Peek()!.Keyword == Keyword.Internal || Peek()!.Keyword == Keyword.Public;

                    AccessModifier accessModifier = AccessModifier.Private; // Default to private
                    if (hasAccessModifier)
                    {
                        if (Peek()!.Keyword == Keyword.Internal)
                            accessModifier = AccessModifier.Internal;
                        if (Peek()!.Keyword == Keyword.Public)
                            accessModifier = AccessModifier.Public;

                        Advance();
                    }
                    
                    bool isExported = Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Export;
                    if (isExported)
                    {
                        Advance(); // Consume the export keyword
                    }

                    bool isVariadic = false;
                    bool isEntryPoint = false;
                    if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Entrypoint)
                    {
                        isEntryPoint = true;
                        Advance();
                    }

                    var functionType = GetMemberedType();
                    var functionName = Peek(1)!.Value;

                    Advance();
                    Advance();
                    Advance(); // We are now on the start of the arguments, if it is not ) there are arguments in the list.
                    var arguments = GetArguments(ref isVariadic);

                    if (Match(Punctuation.Semicolon))
                    {
                        // Create the declaration, and move on.
                        if (isEntryPoint)
                        {
                            throw new Exception("Entry point must contain code block!");
                        }

                        if (isExported)
                        {
                            throw new Exception("Exported functions must contain code block!");
                        }

                        _currentNodes.Add(new FunctionDeclarationExpression(functionName, arguments, functionType,
                            accessModifier,
                            new (),isVariadic,GetLineNumber(), GetColumnNumber(), false, false));
                        continue;
                    }

                    // Handle code block.
                    Advance(); // Consume the {
                    var expressions = ParseCodeBlock(); // Also consumes the }
                    _currentNodes.Add(new FunctionDeclarationExpression(functionName, arguments, functionType, accessModifier, expressions, isVariadic, GetLineNumber(), GetColumnNumber(), isEntryPoint, isExported || isEntryPoint));
                    continue;
                }


                throw new Exception($"Unexpected token \"{Peek()!.Value}\" at {Peek()!.Line}:{Peek()!.Column}");
            }
        }
        catch (Exception ex)
        {
            return new ParserResult(ParserError.InvalidArgument, null, ex.Message);
        }

        sw.Stop();
        _milliseconds = sw.ElapsedMilliseconds;
        return new ParserResult(ParserError.Success, _nodes, null);
    }

    private int GetLineNumber()
    {
        if (Peek() == null)
            return -1;
        return Peek()!.Line;
    }
    
    private int GetColumnNumber()
    {
        if (Peek() == null)
            return -1;
        return Peek()!.Column;
    }

    private IHasType GetMemberedType()
    {
        Expression type = null!;
        if (Peek()!.Type == TokenType.Identifier)
        {
            // Most likely the type identifier, 
            type = new LiteralTypeExpression(Peek()!.Value, GetLineNumber(), GetColumnNumber());
        }
        while (Peek(1)!.Type == TokenType.Operator &&
               Peek(1)!.Operator == Operator.MemberAccess)
        {
            Advance(); 
            type = new MemberAccessExpression(type, Peek(1)!.Value, GetLineNumber(), GetColumnNumber());
            Advance();
        }


        
        return (IHasType) type;
    }

    private bool IsNamespaceStart()
    {
        // Check if an access modifier is present.
        bool hasAccessModifier = Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Public ||
                                 Peek()!.Keyword == Keyword.Private || Peek()!.Keyword == Keyword.Internal;
        int offset = hasAccessModifier ? 1 : 0;
        if (Peek(offset)?.Type != TokenType.Keyword || Peek(offset)!.Keyword != Keyword.Namespace)
            return false;
        if (Peek(1 + offset)?.Type != TokenType.Identifier)
            return false;
        if (Peek(2 + offset)?.Type != TokenType.Punctuation || Peek(2 + offset)!.Punctuation != Punctuation.LeftBrace)
            return false;
        return true;
    }

    private List<VariableDeclarationExpression> GetArguments(ref bool isVariadic)
    {
        List<VariableDeclarationExpression> arguments = new List<VariableDeclarationExpression>();

        if (Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == Punctuation.RightParenthesis)
        {
            Advance();
        }
        else
        {
            // We have arguments, handle them.
            while (!Match(Punctuation.RightParenthesis))
            {
                if (Peek()!.Type == TokenType.Operator && Peek(0)!.Operator == Operator.Variadic)
                {
                    isVariadic = true;
                    Advance(); // Consume the variadic operator
                }
                else
                {
                    var argType = GetMemberedType();
                    var argName = Peek(1)!.Value;
                    
                    if (argType is not IHasType)
                        throw new Exception($"Expected a type for argument \"{argName}\" at {GetLineNumber()}:{GetColumnNumber()}");
                    
                    var isCommaOrEnd = Peek(2)!.Type == TokenType.Punctuation &&
                                       Peek(2)!.Punctuation == Punctuation.Comma ||
                                       Peek(2)!.Punctuation == Punctuation.RightParenthesis;


                    if (!isCommaOrEnd)
                    {
                        throw new Exception("Argument missing closing ) or , seperator");
                    }

                    arguments.Add(new VariableDeclarationExpression(argName, (IHasType)argType, GetLineNumber(), GetColumnNumber()));
                    Advance();
                    Advance();
                    if (Peek()!.Punctuation == Punctuation.RightParenthesis)
                        continue;
                    Advance();
                }
            }
        }

        return arguments;
    }

    private bool IsClassStart()
    {
        bool hasAccessModifier = Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Public ||
                                 Peek()!.Keyword == Keyword.Private || Peek()!.Keyword == Keyword.Internal;

        int offset = hasAccessModifier ? 1 : 0;
        
        if (Peek(offset)?.Type != TokenType.Keyword || Peek(offset)!.Keyword != Keyword.Class)
            return false;
        if (Peek(1 + offset)?.Type != TokenType.Identifier)
            return false;
        if (Peek(2 + offset)?.Type != TokenType.Punctuation || Peek(2 + offset)!.Punctuation != Punctuation.LeftBrace)
            return false;
        return true;
    }

    private List<Expression> ParseCodeBlock()
    {
        List<Expression> expressions = new();
        while (!Match(Punctuation.RightBrace))
        {
            if (Check(TokenType.Comment) || Check(TokenType.Whitespace))
            {
                Advance();
                continue;
            }
            
            if (IsVariableDeclaration())
            {
                var varType = GetMemberedType();
                var varName = Peek(1)!.Value;
                if (varType is not IHasType)
                    throw new Exception($"Expected a type for variable \"{varName}\" at {GetLineNumber()}:{GetColumnNumber()}");

                Advance();
                Advance();

                if (Match(Punctuation.Semicolon))
                {
                    expressions.Add(new VariableDeclarationExpression(varName, (IHasType)varType, GetLineNumber(), GetColumnNumber()));
                    continue;
                }

                bool isAssign = Peek()!.Type == TokenType.Operator && Peek()!.Operator == Operator.Assignment;
                if (!isAssign)
                {
                    throw new Exception("Expected \"=\" after variable declaration!");
                }
                
                Advance();
                
                // Get the rhs
                var rhs = ParseExpression();
                var lhs = new VariableDeclarationExpression(varName, (IHasType)varType, GetLineNumber(), GetColumnNumber());
                expressions.Add(new AssignmentExpression(lhs, rhs, GetLineNumber(), GetColumnNumber()));
                if (!Match(Punctuation.Semicolon))
                {
                    throw new Exception("Expected \";\" at the end of expression!");
                }
                continue;
            }
            
            if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Return)
            {
                Advance(); // Consume the return keyword
                var returnExpr = ParseExpression();
                if (!Match(Punctuation.Semicolon))
                    throw new Exception("Expected \";\" after return expression.");
                expressions.Add(new ReturnExpression(returnExpr, GetLineNumber(), GetColumnNumber()));
                continue;
            }

            if (Match(Keyword.If))
            {
                // Get the condition
                try
                {
                    var ifExpr = ParseIfStatement();
                    _currentNodes.Add(ifExpr);
                    expressions.Add(ifExpr);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error parsing if statement at {Peek()!.Line}:{Peek()!.Column}: {ex.Message}");
                }
                continue;
            }
            
            if (Match(Keyword.While))
            {
                if (!Match(Punctuation.LeftParenthesis))
                    throw new Exception("Expected \"(\" after \"while\" keyword.");
                var condition = ParseExpression();
                if (!Match(Punctuation.RightParenthesis))
                    throw new Exception("Expected \")\" after \"while\" condition.");
                if (!Match(Punctuation.LeftBrace))
                    throw new Exception("Expected \"{\" after \"while\" condition.");
                var body = ParseCodeBlock();
                expressions.Add(new WhileExpression(condition, body, GetLineNumber(), GetColumnNumber()));
                continue;
            }
            if (Match(Keyword.For))
            {
                if (!Match(Punctuation.LeftParenthesis))
                    throw new Exception("Expected \"(\" after \"for\" keyword.");
                var initializer = ParseExpression();
                if (!Match(Punctuation.Semicolon))
                    throw new Exception("Expected \";\" after for initializer.");
                var condition = ParseExpression();
                if (!Match(Punctuation.Semicolon))
                    throw new Exception("Expected \";\" after for condition.");
                var increment = ParseExpression();
                if (!Match(Punctuation.RightParenthesis))
                    throw new Exception("Expected \")\" after for increment.");
                if (!Match(Punctuation.LeftBrace))
                    throw new Exception("Expected \"{\" after for loop.");

                var body = ParseCodeBlock();
                expressions.Add(new ForExpression(initializer, condition, increment, body, GetLineNumber(), GetColumnNumber()));
                continue;
            }
            if (Match(Keyword.Do))
            {
                if (!Match(Punctuation.LeftBrace))
                    throw new Exception("Expected \"{\" after \"do\" keyword.");
                var body = ParseCodeBlock();
                if (!Match(Keyword.While))
                    throw new Exception("Expected \"while\" after \"do\" block.");
                if (!Match(Punctuation.LeftParenthesis))
                    throw new Exception("Expected \"(\" after \"while\" keyword.");
                var condition = ParseExpression();
                if (!Match(Punctuation.RightParenthesis))
                    throw new Exception("Expected \")\" after \"while\" condition.");
                if (!Match(Punctuation.Semicolon))
                    throw new Exception("Expected \";\" after \"while\" condition.");
                
                expressions.Add(new DoWhileExpression(condition, body, GetLineNumber(), GetColumnNumber()));
                continue;
            }
            
            try
            {
                var expr = ParseExpression();
                if (!Match(Punctuation.Semicolon))
                    throw new Exception("Expected \";\" after expression.");
                expressions.Add(expr);
                continue;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing expression at {Peek()!.Line}:{Peek()!.Column}: {ex.Message}");
            }
        }
        
        // Done!
        return expressions;
    }

    private IfExpression ParseIfStatement()
    {
        if (!Match(Punctuation.LeftParenthesis))
            throw new Exception("Expected \"(\" after \"if\" keyword.");
        var condition = ParseExpression();
        if (!Match(Punctuation.RightParenthesis))
            throw new Exception("Expected \")\" after \"if\" condition.");
        if (!Match(Punctuation.LeftBrace))
            throw new Exception("Expected \"{\" after \"if\" condition.");
        var ifBlock = ParseCodeBlock();
        List<Expression> elseBlock = new List<Expression>();
        // Check for the optional else block
        IfExpression? elseIf = null;
        if (Match(Keyword.Else))
        {
            if (Match(Keyword.If))
            {
                elseIf = ParseIfStatement(); // nicely nested
                elseBlock = new List<Expression> { elseIf };
            }
            else
            {
                if (!Match(Punctuation.LeftBrace))
                    throw new Exception("Expected \"{\" or \"if\" after \"else\" keyword.");
                elseBlock = ParseCodeBlock();
            }
        }

        return new IfExpression(condition, ifBlock, GetLineNumber(), GetColumnNumber(), elseBlock);
    }

    private Expression ParseExpression(float parentPrecedence = 0.0f)
    {
        Expression left;

        if (Peek()!.Type == TokenType.Operator && IsPrefixUnary(Peek()!.Operator!.Value))
        {
            var op = Peek()!.Operator!.Value;
            Advance();
            var operand = ParseExpression();
            return new UnaryExpression(op, operand, false, GetLineNumber(), GetColumnNumber());
        }

        left = ParsePrimary();

        if (Peek()!.Type == TokenType.Identifier)
        {
            // We are probably looking at a variable or function call.
            var identifier = Peek()!.Value;
            Advance();
            // Check if it is an equals sign or a semicolon.
            if (Match(Operator.Assignment))
            {
                // This is an assignment.
                var right = ParseExpression();
                
                if (left is not IHasType)
                    throw new Exception($"Expected a type for variable \"{identifier}\" at {GetLineNumber()}:{GetColumnNumber()}");
                left = new AssignmentExpression(new VariableDeclarationExpression(identifier, (IHasType)left, GetLineNumber(), GetColumnNumber()), right, GetLineNumber(), GetColumnNumber());
            }
        }
        
        while (Peek()!.Type == TokenType.Operator && IsPostfixUnary(Peek()!.Operator!.Value))
        {
            var op = Peek()!.Operator!.Value;
            Advance();
            left = new UnaryExpression(op, left, true, GetLineNumber(), GetColumnNumber());
        }
            
        while (Peek()!.Type == TokenType.Operator && PrecedenceMap.TryGetValue(Peek()!.Operator!.Value, out var precedence))
        {
            if (precedence.LeftPrecedence < parentPrecedence)
                break;

            var op = Peek()!.Operator!.Value;
            Advance();

            var right = ParseExpression(precedence.RightPrecedence);
            if (op == Operator.Assignment)
            {
                left = new AssignmentExpression(left, right, GetLineNumber(), GetColumnNumber());
            }
            else
            {
                left = new BinaryExpression(left, op, right, GetLineNumber(), GetColumnNumber());
            }
        }

        return left;
    }

    private Expression ParsePrimary()
    {
        Expression expr;

        var token = Peek()!;

        if (token.Type == TokenType.Punctuation && token.Punctuation == Punctuation.LeftParenthesis)
        {
            Advance();
            expr = ParseExpression();
            if (!Match(Punctuation.RightParenthesis))
                throw new Exception($"Expected \")\" after expression at {Peek()!.Line}:{Peek()!.Column}");
        }
        else if (token.Type == TokenType.Literal)
        {
            Advance();
            expr = new LiteralExpression(token.Value, token.Literal!.Value, GetLineNumber(), GetColumnNumber());
        }
        else if (token.Type == TokenType.Identifier)
        {
            Advance();
            expr = new IdentifierExpression(token.Value, GetLineNumber(), GetColumnNumber());
        }
        else if (token.Type == TokenType.Keyword && token.Keyword == Keyword.New)
        {
            Advance(); // Consume the "new" keyword
            if (Peek()!.Type != TokenType.Identifier)
                throw new Exception($"Expected type identifier after \"new\" at {Peek()!.Line}:{Peek()!.Column}");
            var typeName = GetMemberedType();
            Advance(); // Consume the type identifier

            if (!Match(Punctuation.LeftParenthesis))
                throw new Exception($"Expected \"(\" after type identifier at {Peek()!.Line}:{Peek()!.Column}");

            var args = new List<Expression>();
            if (Peek()!.Punctuation != Punctuation.RightParenthesis)
            {
                do
                {
                    args.Add(ParseExpression());
                } while (Match(Punctuation.Comma));
            }

            if (!Match(Punctuation.RightParenthesis))
                throw new Exception("Expected \")\" after constructor arguments");

            if (typeName is not IHasType)
                throw new Exception($"Expected a type for \"new {typeName}\" at {GetLineNumber()}:{GetColumnNumber()}");
            expr = new NewExpression((IHasType)typeName, args, GetLineNumber(), GetColumnNumber());
        }
        else if (token.Type == TokenType.Keyword && token.Keyword == Keyword.Destroy)
        {
            Advance(); // Consume the "destroy" keyword
            if (Peek()!.Type != TokenType.Identifier)
                throw new Exception($"Expected identifier after \"destroy\" at {Peek()!.Line}:{Peek()!.Column}");
            var identifier = Peek()!.Value;
            Advance(); // Consume the identifier
            
            expr = new DestroyExpression(identifier, GetLineNumber(), GetColumnNumber());
        }
        else if (token.Type == TokenType.Keyword && token.Keyword == Keyword.Null)
        {
            Advance();
            expr = new LiteralExpression("null", Literal.Null, GetLineNumber(), GetColumnNumber());
        }
        else if (Match(Keyword.True))
        {
            expr = new LiteralExpression("1", Literal.Int, GetLineNumber(), GetColumnNumber()); // TODO: Maybe use a better representation for booleans?
        }
        else if (Match(Keyword.False))
        {
            expr = new LiteralExpression("0", Literal.Int, GetLineNumber(), GetColumnNumber()); // TODO: Maybe use a better representation for booleans?
        }
        else if (Match(Keyword.Continue))
        {
            expr = new ContinueExpression(GetLineNumber(), GetColumnNumber());
        }
        else if (Match(Operator.Variadic))
        {
            expr = new VariadicExpression(GetLineNumber(), GetColumnNumber());
        }
        else if (Match(Keyword.Break))
        {
            expr = new BreakExpression(GetLineNumber(), GetColumnNumber());
        }
        else if (Match(Keyword.This))
        {
            // This is a reference to the current instance in a class.
            if (!_isInClass)
                throw new Exception("The \"this\" keyword can only be used within a class scope.");
            expr = new ThisExpression(GetLineNumber(), GetColumnNumber());
        }
        else
        {
            throw new Exception(
                $"Unexpected token \"{token.Value}\" at {token.Line}:{token.Column}. Expected a primary expression.");
        }

        // Now handle member access and calls
        while (true)
        {
            if (Peek()!.Type == TokenType.Operator && Peek()!.Operator == Operator.MemberAccess)
            {
                Advance(); // consume \".\"
                var memberToken = Peek()!;
                if (memberToken.Type != TokenType.Identifier)
                    throw new Exception("Expected identifier after \".\"");

                Advance(); // consume identifier
                expr = new MemberAccessExpression(expr, memberToken.Value, GetLineNumber(), GetColumnNumber());
            }
            else if (Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == Punctuation.LeftParenthesis)
            {
                // Function call (can be after member access!)
                Advance(); // consume \"(\"
                var args = new List<Expression>();
                if (Peek()!.Punctuation != Punctuation.RightParenthesis)
                {
                    do
                    {
                        args.Add(ParseExpression());
                    } while (Match(Punctuation.Comma));
                }

                if (!Match(Punctuation.RightParenthesis))
                    throw new Exception("Expected \")\" after function call arguments");

                expr = new FunctionCallExpression(expr, args, GetLineNumber(), GetColumnNumber()); // Updated to take an expression target
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private bool IsPrefixUnary(Operator op)
    {
        return op is Operator.Minus or Operator.Not or Operator.BitwiseNot or
            Operator.Increment or Operator.Decrement or Operator.Asterisk or Operator.BitwiseAnd;
    }

    private bool IsPostfixUnary(Operator op)
    {
        return op is Operator.Increment or Operator.Decrement;
    }
    
    private bool IsVariableDeclaration()
    {
        var isSymbol = Peek()!.Type == TokenType.Identifier;
        var isName = Peek(1)!.Type == TokenType.Identifier;
        if (!isSymbol || !isName)
            return false;
        
        return true;
    }

    private bool IsFunctionStart()
    {
        int oldPosition = _position;
        
        // First check for an access modifier
        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Private ||
            Peek()!.Keyword == Keyword.Internal || Peek()!.Keyword == Keyword.Public)
        {
            Advance();
        }

        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Export)
        {
            Advance();
        }
        
        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Entrypoint)
        {
            // This is an entrypoint function
            Advance(); // Consume the entrypoint
        }

        // Check if the current type && next identifier exists.
        var type = GetMemberedType();
        Advance();
        bool hasTypeAndIdentifier = Peek()!.Type == TokenType.Identifier && type is IHasType;
        Advance();
        if (!hasTypeAndIdentifier)
        {
            _position = oldPosition;
            return false;
        }
        

        bool hasArgList = false;
        if (Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == Punctuation.LeftParenthesis)
        {
            Advance();
            while (!Match(Punctuation.RightParenthesis))
            {
                Advance();
            }

            hasArgList = true;
        }

        bool hasCodeBlockOrSemicolon = Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == Punctuation.Semicolon ||
                                       Peek()!.Punctuation == Punctuation.LeftBrace;

        _position = oldPosition;

        return hasTypeAndIdentifier && hasArgList && hasCodeBlockOrSemicolon;
    }

    private bool Match(Keyword keyword)
    {
        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == keyword)
        {
            Advance();
            return true;
        }

        return false;
    }
    
    private bool Match(Literal literal)
    {
        if (Peek()!.Type == TokenType.Literal && Peek()!.Literal == literal)
        {
            Advance();
            return true;
        }

        return false;
    }
    
    private bool Match(Operator @operator)
    {
        if (Peek()!.Type == TokenType.Operator && Peek()!.Operator == @operator)
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Match(Punctuation punctuation)
    {
        if (Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == punctuation)
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Check(TokenType type)
    {
        return !IsAtEnd() && Peek()!.Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _position++;
        return Previous();
    }

    private bool IsAtEnd() => _position >= _tokens.Count;

    private Token? Peek(int offset = 0) => offset+_position >= _tokens.Count ? null : _tokens[_position+offset];

    private Token Previous() => _tokens[_position - 1];
    
    public string GetDebugInfo(DebugLevel level = DebugLevel.Debug)
    {
        StringBuilder sb = new();

        if (level <= DebugLevel.Debug)
        {
            foreach (var node in _nodes)
            {
                sb.AppendLine(node.GetStringRepresentation());
            }
        }
        
        if (level <= DebugLevel.Info)
        {
            sb.AppendLine($"Parser took {_milliseconds}ms to parse {_tokens.Count} tokens. Rate of {(float)_tokens.Count / ((float)_milliseconds / 1000.0f)} tokens per second.");
        }

        return sb.ToString();
    }
}