using System.Text;

namespace RumLang.Tokenizer;

public enum TokenType
{
    Keyword,
    Literal,
    Operator,
    Punctuation,
    Symbol,
    Comment,
    Whitespace
}

public class Token
{
    public TokenType Type { get; }
    public Keyword? Keyword { get; }
    public Literal? Literal { get; }
    public Operator? Operator { get; }
    public Punctuation? Punctuation { get; }
    public Symbol? Symbol { get; }

    public string Value { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(Keyword keyword, string value, int line, int column)
        : this(TokenType.Keyword, keyword, null, null, null, null, value, line, column) { }

    public Token(Literal literal, string value, int line, int column)
        : this(TokenType.Literal, null, literal, null, null, null, value, line, column) { }

    public Token(Operator @operator, string value, int line, int column)
        : this(TokenType.Operator, null, null, @operator, null, null, value, line, column) { }

    public Token(Punctuation punctuation, string value, int line, int column)
        : this(TokenType.Punctuation, null, null, null, punctuation, null, value, line, column) { }

    public Token(Symbol symbol, string value, int line, int column)
        : this(TokenType.Symbol, null, null, null, null, symbol, value, line, column) { }

    public Token(TokenType type, Keyword? keyword, Literal? literal, Operator? @operator,
        Punctuation? punctuation, Symbol? symbol, string value, int line, int column)
    {
        Type = type;
        Keyword = keyword;
        Literal = literal;
        Operator = @operator;
        Punctuation = punctuation;
        Symbol = symbol;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append($"{Type.ToString()} ");
        if (Type == TokenType.Punctuation)
            sb.Append($"{Punctuation.ToString()} ");
        if (Type == TokenType.Keyword)
            sb.Append($"{Keyword.ToString()} ");
        if (Type == TokenType.Literal)
            sb.Append($"{Literal.ToString()} ");
        if (Type == TokenType.Operator)
            sb.Append($"{Operator.ToString()} ");
        if (Type == TokenType.Symbol)
            sb.Append($"{Symbol.ToString()} ");

        sb.Append($"\"{Value}\" at ({Line}:{Column})");
        return sb.ToString();
    }
}