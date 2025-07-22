using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class UnaryExpression : Expression
{
    public Operator Operator { get; }
    public Expression Value { get; }
    public bool IsPostfix { get; }

    public UnaryExpression(Operator @operator, Expression expression, bool isPostfix)
    {
        Operator = @operator;
        Value = expression;
        IsPostfix = isPostfix;
    }

    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}UnaryExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Value: \n{Value.GetStringRepresentation(depth+1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Operator: {Operator.ToString()}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Postfix: {IsPostfix}");
        return sb.ToString();
    }
}