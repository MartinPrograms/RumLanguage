global using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class MemberAccessExpression : Expression, IHasType, IFlattenable
{
    public Expression Target { get; }
    public string MemberName { get; }

    public Literal TypeLiteral => Literal.Custom;
    public Expression TypeExpression => Target;
    
    public MemberAccessExpression(Expression target, string memberName, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
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
    
    public string Flatten()
    {
        StringBuilder sb = new();
        sb.Append(Target is IFlattenable flattenable ? flattenable.Flatten() : "<unknown>");
        sb.Append($".{MemberName}");
        return sb.ToString();
    }
}