namespace RumLang.Parser.Definitions;

public class VariadicExpression : Expression
{
    public VariadicExpression(int lineNumber, int columnNumber) 
    : base(lineNumber, columnNumber)
    {
        
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}VariadicExpression");
        return sb.ToString();
    }
}