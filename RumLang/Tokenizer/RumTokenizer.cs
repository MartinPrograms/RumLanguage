using System.Diagnostics;
using System.Text;

namespace RumLang.Tokenizer;

public enum TokenizerError
{
    Success,
    UnexpectedToken,
    UnexpectedEof
}

public record TokenizerResult(TokenizerError Error, List<Token>? Tokens, string? ErrorString);

// Takes in source code, turns it into a list of tokens!
public class RumTokenizer : IDebugInfo
{
    private int _position = 0;
    private int _line = 1;
    private int _column = 1;
    private long _milliseconds = 0;
    private List<Token> _tokens = new List<Token>();
    private string _source = string.Empty;
 
    private static readonly Dictionary<string, Keyword> _keywords = Enum.GetValues<Keyword>().ToDictionary(k => k.ToString().ToLower(), k => k);
    private static readonly Dictionary<string, Operator> _operators = new()
    {
        { "==", Operator.Equal },
        { "!=", Operator.NotEqual },
        { ">=", Operator.GreaterThanOrEqual },
        { "<=", Operator.LessThanOrEqual },
        { "&&", Operator.And },
        { "||", Operator.Or },
        { "++", Operator.Increment },
        { "--", Operator.Decrement },
        { "+", Operator.Plus },
        { "-", Operator.Minus },
        { "*", Operator.Asterisk },
        { "/", Operator.Divide },
        { "%", Operator.Modulus },
        { "=", Operator.Assignment },
        { ">", Operator.GreaterThan },
        { "<", Operator.LessThan },
        { "!", Operator.Not },
        { "&", Operator.BitwiseAnd },
        { "|", Operator.BitwiseOr },
        { "^", Operator.BitwiseXor },
        { "~", Operator.BitwiseNot },
        { "<<", Operator.LeftShift },
        { ">>", Operator.RightShift },
        { "->", Operator.PointerAccess }, // Pointer access operator
        { ".", Operator.MemberAccess }, // Member access operator
        { "...", Operator.Variadic } // Variadic operator
    };
    
    private static readonly Dictionary<char, Punctuation> _punctuation = new()
    {
        { ';', Punctuation.Semicolon },
        { ',', Punctuation.Comma },
        { '(', Punctuation.LeftParenthesis },
        { ')', Punctuation.RightParenthesis },
        { '{', Punctuation.LeftBrace },
        { '}', Punctuation.RightBrace },
        { '[', Punctuation.LeftBracket },
        { ']', Punctuation.RightBracket },
    };
    
    public TokenizerResult Tokenize(string source)
    {
        Stopwatch sw = Stopwatch.StartNew();
        
        _source = source;
        _line = 1;
        _column = 1;
        _tokens.Clear();
        
        while (!IsAtEnd())
        {
            char current = Peek();

            if (char.IsWhiteSpace(current))
            {
                HandleWhitespace();
                continue;
            }
                        
            // Check if the last character is a comment start
            if (current == '/' && Peek(1) == '/')
            {
                // Single-line comment
                int start = _position;
                while (!IsAtEnd() && Peek() != '\n')
                    Advance();
                _tokens.Add(new Token(TokenType.Comment, null, null, null, null, _source[start.._position], _line, _column));
                continue;
            }
            
            if (current == '/' && Peek(1) == '*')
            {
                // Multi-line comment
                Advance(); // Skip first '/'
                Advance(); // Skip '*'
                int start = _position;
                while (!IsAtEnd() && !(Peek() == '*' && Peek(1) == '/'))
                {
                    if (Peek() == '\n') _line++;
                    Advance();
                }
                
                if (IsAtEnd())
                    return new TokenizerResult(TokenizerError.UnexpectedEof, null, "Unterminated multi-line comment");

                Advance(); // Skip '*'
                Advance(); // Skip '/'
                
                _tokens.Add(new Token(TokenType.Comment, null, null, null, null, _source[start.._position], _line, _column));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                ReadIdentifierOrKeyword();
                continue;
            }

            if (char.IsDigit(current))
            {
                ReadNumber();
                continue;
            }

            if (current == '"')
            {
                var result = ReadString();
                if (result != null)
                    _tokens.Add(result);
                else
                    return new TokenizerResult(TokenizerError.UnexpectedEof, null, "Unterminated string literal");
                continue;
            }

            if (TryMatchOperator(out var opToken))
            {
                _tokens.Add(opToken);
                continue;
            }

            if (_punctuation.TryGetValue(current, out var punct))
            {
                _tokens.Add(new Token(punct, current.ToString(), _line, _column));
                Advance();
                continue;
            }
            
            return new TokenizerResult(TokenizerError.UnexpectedToken, null, $"Unexpected character '{current}' at line {_line}, column {_column}");
        }

        sw.Stop();
        _milliseconds = sw.ElapsedMilliseconds;
        return new TokenizerResult(TokenizerError.Success, _tokens, null);
    }
    
    private void HandleWhitespace()
    {
        if (Peek() == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        Advance();
    }
    private void ReadIdentifierOrKeyword()
    {
        int start = _position;
        int col = _column;

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            Advance();

        string text = _source[start.._position];

        if (_keywords.TryGetValue(text.ToLower(), out var keyword))
        {
            _tokens.Add(new Token(keyword, text, _line, col));
        }
        else
        {
            _tokens.Add(new Token(TokenType.Identifier, null, null, null, null, text, _line, col));
        }
    }
    private void ReadNumber()
    {
        int start = _position;
        int col = _column;
        bool hasDot = false;

        while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.'))
        {
            if (Peek() == '.')
            {
                if (hasDot) break; // second dot, stop
                hasDot = true;
            }
            Advance();
        }

        string number = _source[start.._position];
        var literalType = hasDot ? Literal.Double : Literal.Long; // Later will be cast down to either int or float if specified
        _tokens.Add(new Token(literalType, number, _line, col));
    }
    
    private Token? ReadString()
    {
        int col = _column;
        Advance(); // Skip opening "

        var sb = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\n') _line++;
            sb.Append(Peek());
            Advance();
        }

        if (IsAtEnd()) return null;

        Advance(); // Skip closing "

        return new Token(Literal.String, sb.ToString(), _line, col);
    }
    
    private bool TryMatchOperator(out Token token)
    {
        int col = _column;
        foreach (var op in _operators.Keys.OrderByDescending(k => k.Length))
        {
            if (Match(op))
            {
                token = new Token(_operators[op], op, _line, col);
                return true;
            }
        }

        token = null!;
        return false;
    }
    private bool Match(string text)
    {
        if (_position + text.Length > _source.Length) return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (_source[_position + i] != text[i]) return false;
        }

        for (int i = 0; i < text.Length; i++)
            Advance();

        return true;
    }

    private char Peek(int offset = 0) => _position + offset < _source.Length ? _source[_position + offset] : '\0';

    private void Advance()
    {
        _position++;
        _column++;
    }

    private bool IsAtEnd() => _position >= _source.Length;

    public string GetDebugInfo(DebugLevel level = DebugLevel.Debug)
    {
        StringBuilder sb = new StringBuilder();
        if (level <= DebugLevel.Debug)
        {
            foreach (var token in _tokens)
            {
                sb.AppendLine(
                    $"{StringHelpers.AlignTo(token.Type.ToString(), 11, true)} : {StringHelpers.AlignTo(token.Value, 20)} ({token.Line}:{token.Column})");
            }
        }
        
        if (level <= DebugLevel.Info)
        {
            sb.AppendLine(
                $"Tokenizer parsed in {_milliseconds}ms & parsed {_tokens.Count} tokens. Rate of {(float)_tokens.Count / ((float)_milliseconds / 1000.0f)} tokens per second.");
        }

        return sb.ToString();
    }
}