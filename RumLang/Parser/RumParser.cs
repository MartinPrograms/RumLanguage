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

                    _nodes.Add(new ImportExpression(sb.ToString()));

                    continue;
                }

                if (Match(Keyword.Namespace))
                {
                    var namespaceName = Peek()!.Value;
                    var nodes = new List<AstNode>();
                    _currentNodes.Add(new NamespaceDeclarationExpression(namespaceName, nodes));
                    _currentNodes = nodes;
                    _namespaces.Push((namespaceName, nodes));
                    Advance();
                    if (!Match(Punctuation.LeftBrace))
                        throw new Exception("Expected \"{\" after namespace identifier!");
                    
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

                    bool isEntryPoint = false;
                    if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Entrypoint)
                    {
                        isEntryPoint = true;
                        Advance();
                    }

                    var functionType = Peek()!.Value;
                    var functionName = Peek(1)!.Value;

                    Advance();
                    Advance();
                    Advance(); // We are now on the start of the arguments, if it is not ) there are arguments in the list.
                    List<Expression> arguments = new List<Expression>();

                    if (Peek()!.Type == TokenType.Punctuation && Peek()!.Punctuation == Punctuation.RightParenthesis)
                    {
                        Advance();
                    }
                    else
                    {
                        // We have arguments, handle them.
                        while (!Match(Punctuation.RightParenthesis))
                        {
                            // type identifier, (comma)
                            var argType = Peek()!.Value;
                            var argName = Peek(1)!.Value;
                            var isCommaOrEnd = Peek(2)!.Type == TokenType.Punctuation &&
                                               Peek(2)!.Punctuation == Punctuation.Comma ||
                                               Peek(2)!.Punctuation == Punctuation.RightParenthesis;

                            if (!isCommaOrEnd)
                            {
                                throw new Exception("Argument missing closing ) or , seperator");
                            }

                            arguments.Add(new VariableDeclarationExpression(argName, argType));
                            Advance();
                            Advance();
                            if (Peek()!.Punctuation == Punctuation.RightParenthesis)
                                continue;
                            Advance();
                        }
                    }

                    if (Match(Punctuation.Semicolon))
                    {
                        // Create the declaration, and move on.
                        if (isEntryPoint)
                        {
                            throw new Exception("Entry point must contain code block!");
                        }

                        _currentNodes.Add(new FunctionDeclarationExpression(functionName, arguments, functionType,
                            accessModifier,
                            new (),false));
                        continue;
                    }

                    // Handle code block.
                    Advance(); // Consume the {
                    var expressions = ParseCodeBlock(); // Also consumes the }
                    _currentNodes.Add(new FunctionDeclarationExpression(functionName, arguments, functionType, accessModifier, expressions, isEntryPoint));
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
                var varType = Peek()!.Value;
                var varName = Peek(1)!.Value;

                Advance();
                Advance();

                if (Match(Punctuation.Semicolon))
                {
                    expressions.Add(new VariableDeclarationExpression(varName, varType));
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
                var lhs = new VariableDeclarationExpression(varName, varType);
                expressions.Add(new AssignmentExpression(lhs, rhs));
                if (!Match(Punctuation.Semicolon))
                {
                    throw new Exception("Expected \";\" at the end of expression!");
                }
                continue;
            }

            throw new Exception($"Unexpected token \"{Peek()!.Value}\" at {Peek()!.Line}:{Peek()!.Column}");
        }
        
        // Done!
        return expressions;
    }

    private Expression ParseExpression(float parentPrecedence = 0.0f)
    {
        Expression left;

        if (Peek()!.Type == TokenType.Operator && IsPrefixUnary(Peek()!.Operator!.Value))
        {
            var op = Peek()!.Operator!.Value;
            Advance();
            var operand = ParseExpression();
            return new UnaryExpression(op, operand, isPostfix: false);
        }

        left = ParsePrimary();

        while (Peek()!.Type == TokenType.Operator && IsPostfixUnary(Peek()!.Operator!.Value))
        {
            var op = Peek()!.Operator!.Value;
            Advance();
            left = new UnaryExpression(op, left, isPostfix: true);
        }
            
        while (Peek()!.Type == TokenType.Operator && PrecedenceMap.TryGetValue(Peek()!.Operator!.Value, out var precedence))
        {
            if (precedence.LeftPrecedence < parentPrecedence)
                break;

            var op = Peek()!.Operator!.Value;
            Advance();

            var right = ParseExpression(precedence.RightPrecedence); // Right-associativity
            left = new BinaryExpression(left, op, right);
        }

        return left;
    }
    
    private Expression ParsePrimary()
    {
        var token = Peek()!;
        
        if (token.Type == TokenType.Punctuation && token.Punctuation == Punctuation.LeftParenthesis)
        {
            Advance();
            var inner = ParseExpression();

            if (!Match(Punctuation.RightParenthesis))
                throw new Exception($"Expected ')' after expression at {Peek()!.Line}:{Peek()!.Column}");

            return inner;
        }
        
        if (token.Type == TokenType.Literal)
        {
            Advance();
            return new LiteralExpression(token.Value, token.Literal!.Value);
        }
    
        if (token.Type == TokenType.Identifier)
        {
            Advance();
            return new IdentifierExpression(token.Value);
        }

        throw new Exception($"Unexpected token {token.Value}");
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
        
        if (Peek()!.Type == TokenType.Keyword && Peek()!.Keyword == Keyword.Entrypoint)
        {
            // This is an entrypoint function
            Advance(); // Consume the entrypoint
        }

        // Check if the current type && next identifier exists.
        bool hasTypeAndIdentifier = false;
        if (Peek()!.Type == TokenType.Identifier && Peek(1)!.Type == TokenType.Identifier)
        {
            hasTypeAndIdentifier = true;
            Advance(); // Consume type
            Advance(); // Consume identifier
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

        return sb.ToString();
    }
}