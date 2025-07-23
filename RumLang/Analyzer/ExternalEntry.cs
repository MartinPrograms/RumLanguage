using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Analyzer;

public class ExternalEntry
{
    public List<AnalyzerNamespace> Namespaces { get; set; }

    public ExternalEntry(Rum rum, string source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source), "Source code cannot be null.");
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source code cannot be empty.", nameof(source));
        
        var (error, tokens, errorString, tokenizer) = rum.Tokenize(source);
        if (error != TokenizerError.Success)
            throw new Exception($"Tokenizer error: {errorString}");
        if (tokens == null || tokens.Count == 0)
            throw new Exception("No tokens generated from the source code.");
        
        var (parseError, root, parseErrorString, parser) = rum.Parse(tokens);
        if (parseError != ParserError.Success)
            throw new Exception($"Parser error: {parseErrorString}");
        if (root == null || root.Count == 0)
            throw new Exception("No AST nodes generated from the tokens.");
        
        Namespaces = new List<AnalyzerNamespace>();
        void CrawlNodes (List<AstNode> nodes, AnalyzerNamespace? parentNamespace = null)
        {
            foreach (var node in nodes)
            {
                if (node is NamespaceDeclarationExpression nsd)
                {
                    var ns = new AnalyzerNamespace
                    {
                        Name = nsd.Identifier,
                        AccessModifier = nsd.AccessModifier,
                        ParentNamespace = parentNamespace,
                        SubNamespaces = new List<AnalyzerNamespace>(),
                        Types = new List<AnalyzerType>()
                    };
                    
                    // Add the namespace to the list
                    Namespaces.Add(ns);
                    
                    // Recursively crawl sub-namespaces
                    CrawlNodes(nsd.Nodes, ns);
                }
                else if (node is ClassDeclarationExpression cde)
                {
                    // Handle class declarations similarly
                    var type = new AnalyzerType()
                    {
                        Name = cde.ClassName,
                        AccessModifier = cde.AccessModifier,
                        Functions = cde.Functions,
                        Members = cde.Variables
                    };
                    
                    if (parentNamespace != null)
                        parentNamespace.Types.Add(type);
                }
                else if (node is FunctionDeclarationExpression fde)
                {
                    if (parentNamespace == null)
                        throw new Exception("Function declaration found outside of a namespace or class context.");
                    parentNamespace.Functions.Add(fde);
                }
            }
        }
        
        // Start crawling from the root nodes
        CrawlNodes(root);
    }
}