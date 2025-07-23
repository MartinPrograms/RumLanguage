namespace RumLang.Parser.Definitions;

public class ContinueExpression : Expression
{
    public ContinueExpression()
    {
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        return $"{StringHelpers.Repeat("\t", depth)}ContinueExpression";
    }
}