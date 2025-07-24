namespace RumLang.Parser.Definitions;

public class ThisExpression : Expression, IFlattenable
{
    public ThisExpression(int lineNumber, int columnNumber) 
    : base(lineNumber, columnNumber)
    {
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}ThisExpression";
    }
    
    public string Flatten()
    {
        return "this";
    }
}