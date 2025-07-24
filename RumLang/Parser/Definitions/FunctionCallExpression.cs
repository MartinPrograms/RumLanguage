using System.Text;
using System.Collections.Generic;

namespace RumLang.Parser.Definitions;

public class FunctionCallExpression : Expression, IHasChildren
{
    public Expression FunctionTarget { get; }
    public List<Expression> Arguments { get; }
    
    public FunctionCallExpression(Expression functionTarget, List<Expression> arguments, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        FunctionTarget = functionTarget;
        Arguments = arguments;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}FunctionCallExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Function: \n{FunctionTarget.GetStringRepresentation(depth + 1)}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Arguments: \n{string.Join("\n", Arguments.Select(arg => arg.GetStringRepresentation(depth + 1)))}");

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        List<List<AstNode>> children = new()
        {
            new List<AstNode> { FunctionTarget }
        };
        
        if (Arguments.Count > 0)
        {
            children.Add(Arguments.Cast<AstNode>().ToList());
        }
        
        return children;
    }
}