namespace RumLang.Parser.Definitions;

public class ContinueExpression : Expression
{
    public ContinueExpression(int lineNumber, int columnNumber) 
    : base(lineNumber, columnNumber)
    {
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}ContinueExpression";
    }
}