using System.Text;

namespace RumLang.Parser.Definitions;

/// <summary>
/// Sets LHS to RHS
/// </summary>
public class AssignmentExpression : Expression
{
    public Expression Lhs { get; }
    public Expression Rhs { get; }

    public AssignmentExpression(Expression lhs, Expression rhs)
    {
        Lhs = lhs;
        Rhs = rhs;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}AssignmentExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Lhs: \n{Lhs.GetStringRepresentation(depth+1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Rhs: \n{Rhs.GetStringRepresentation(depth + 1)}");

        return sb.ToString();
    }
}