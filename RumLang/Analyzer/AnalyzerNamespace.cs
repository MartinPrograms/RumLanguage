using RumLang.Parser.Definitions;

namespace RumLang.Analyzer;

public class AnalyzerNamespace
{
    public string Name { get; set; } // e.g., "rum"
    public AccessModifier AccessModifier { get; set; }
    public List<AnalyzerNamespace> SubNamespaces { get; set; } // e.g., "rum.lang.foo.bar"
    public AnalyzerNamespace? ParentNamespace { get; set; } // e.g., "rum.lang.foo"
    public List<AnalyzerType> Types { get; set; }
    public List<FunctionDeclarationExpression> Functions { get; set; }
    
    public AnalyzerNamespace()
    {
        SubNamespaces = new List<AnalyzerNamespace>();
        Types = new List<AnalyzerType>();
        Functions = new List<FunctionDeclarationExpression>();
    }

    public string GetFullName()
    {
        if (ParentNamespace == null)
            return Name;

        return $"{ParentNamespace.GetFullName()}.{Name}";
    }
}