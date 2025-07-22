global using System.Text;

namespace RumLang.Parser.Definitions;

public class MemberAccessExpression : Expression
{
    public Expression Target { get; }
    public string MemberName { get; }
    
    public MemberAccessExpression(Expression target, string memberName)
    {
        Target = target;
        MemberName = memberName;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}MemberAccessExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Target: \n{Target.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- MemberName: {MemberName}");
        
        return sb.ToString();
    }
}