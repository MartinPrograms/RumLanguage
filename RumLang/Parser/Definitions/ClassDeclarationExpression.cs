using RumLang.Tokenizer;

namespace RumLang.Parser.Definitions;

/*
public class test_class
{
   private int private_var;
   
   public test_class(int initial_value)
   {
       private_var = initial_value;
   }
   
   public int get_value()
   {
       return private_var;
   }
   
   public void set_value(int new_value)
   {
       private_var = new_value;
   }
}
 */
public class ClassDeclarationExpression : Expression, IHasChildren, IHasType, IFlattenable
{
    public string ClassName { get; }
    public AccessModifier AccessModifier { get; }
    public List<NamespaceDeclarationExpression> Namespaces { get; }
    public List<FunctionDeclarationExpression> Functions { get; }
    public List<ClassMemberDeclaration> Variables { get; }
    
    public Literal TypeLiteral => Literal.Custom;
    public Expression TypeExpression => this;
    
    public ClassDeclarationExpression(string className, List<NamespaceDeclarationExpression> namespaces, AccessModifier accessModifier,
        List<FunctionDeclarationExpression> functions, List<ClassMemberDeclaration> variables, int lineNumber, int columnNumber) 
        : base(lineNumber, columnNumber)
    {
        ClassName = className;
        AccessModifier = accessModifier;
        Namespaces = namespaces ?? new List<NamespaceDeclarationExpression>();
        Functions = functions;
        Variables = variables;
    }
    
    public override string GetStringRepresentation(int depth = 0)
    {
        StringBuilder sb = new();
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}ClassDeclarationExpression");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- ClassName: {ClassName}");
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- AccessModifier: {AccessModifier.ToString()}");
        
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Functions:");
        foreach (var function in Functions)
        {
            sb.Append(function.GetStringRepresentation(depth + 1));
        }
        
        sb.AppendLine($"{StringHelpers.Repeat("\t", depth)}:- Variables:");
        foreach (var variable in Variables)
        {
            sb.Append(variable.GetStringRepresentation(depth + 1));
        }

        return sb.ToString();
    }
    
    public List<List<AstNode>> GetChildren()
    {
        List<AstNode> children = new();
        children.AddRange(Functions);
        children.AddRange(Variables);
        return new List<List<AstNode>> { children };
    }

    public string Flatten()
    {
        // For each namespace prepend a .
        StringBuilder sb = new();
        foreach (var ns in Namespaces)
        {
            sb.Append(ns.Identifier);
            sb.Append(".");
        }
        sb.Append(ClassName);
        return sb.ToString();
    }
}