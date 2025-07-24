namespace RumLang.Parser;

public abstract class AstNode
{
    public abstract string GetStringRepresentation(int depth = 0);
    public int LineNumber { get; set; } = -1;
    public int ColumnNumber { get; set; } = -1;
}

public abstract class Expression : AstNode
{
    public Expression(int lineNumber, int columnNumber)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }
}