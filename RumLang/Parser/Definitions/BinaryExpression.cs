using System.Text;
using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

public class BinaryExpression : Expression
{
    public Expression Lhs { get; }
    public Operator Operator { get; }
    public Expression Rhs { get; }

    public BinaryExpression(Expression lhs, Operator op, Expression rhs)
    {
        Lhs = lhs;
        Operator = op;
        Rhs = rhs;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}BinaryExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Lhs: \n{Lhs.GetStringRepresentation(depth+1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Operator: {Operator.ToString()}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Rhs: \n{Rhs.GetStringRepresentation(depth+1)}");

        return sb.ToString();
    }
}