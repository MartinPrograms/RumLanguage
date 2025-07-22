namespace RumLang.Parser.Definitions;

/// <summary>
/// if (condition){
///
/// }
/// else {
///
/// }
///
/// where else could lead into another if statement
/// </summary>
public class IfExpression : Expression
{
    public Expression Condition { get; }
    public List<Expression> ThenBranch { get; }
    public List<Expression>? ElseBranch { get; }
    
    public IfExpression(Expression condition, List<Expression> thenBranch, List<Expression>? elseBranch = null)
    {
        Condition = condition;
        ThenBranch = thenBranch;
        ElseBranch = elseBranch;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}IfExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Condition: \n{Condition.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- ThenBranch: \n{string.Join("\n", ThenBranch.Select(x => x.GetStringRepresentation(depth + 1)))}");
        
        if (ElseBranch != null)
        {
            sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- ElseBranch: \n{string.Join("\n", ElseBranch.Select(x => x.GetStringRepresentation(depth + 1)))}");
        }
        return sb.ToString();
    }
}