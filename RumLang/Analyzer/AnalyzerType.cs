using RumLang.Parser.Definitions;

namespace RumLang.Analyzer;

public class AnalyzerType
{
    public string Name { get; set; }
    public AccessModifier AccessModifier { get; set; }
    public List<ClassMemberDeclaration> Members { get; set; }
    public List<FunctionDeclarationExpression> Functions { get; set; }
}