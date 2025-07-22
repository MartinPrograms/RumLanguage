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

    public RumParser(List<Token> tokens)
    {
        _tokens = tokens;
        _nodes = new List<AstNode>();
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

                if (Match(Keyword.Import))
                {
                    if (_isInFunction)
                    {
                        throw new Exception("Can not import within function scope.");
                    }
                    StringBuilder sb = new();
                    while (!Match(Punctuation.Semicolon))
                    {
                        if (Peek()!.Type == TokenType.Symbol)
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
                        _nodes.Add(new FunctionDeclarationExpression(functionName, arguments, functionType, accessModifier, false));
                        continue;
                    }
                    
                    // Handle code block.
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
        if (Peek()!.Type == TokenType.Symbol && Peek(1)!.Type == TokenType.Symbol)
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
    
    private bool Match(Symbol symbol)
    {
        if (Peek()!.Type == TokenType.Symbol && Peek()!.Symbol == symbol)
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