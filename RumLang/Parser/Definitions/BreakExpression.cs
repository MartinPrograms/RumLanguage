namespace RumLang.Parser.Definitions;

public class BreakExpression : Expression
{
    public BreakExpression()
    {
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}BreakExpression";
    }
}