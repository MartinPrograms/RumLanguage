namespace RumLang.Parser.Definitions;

public class ThisExpression : Expression
{
    public ThisExpression()
    {
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}ThisExpression";
    }
}