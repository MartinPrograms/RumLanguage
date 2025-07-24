using RumLang.Parser;
using RumLang.Parser.Definitions;
using RumLang.Tokenizer;

namespace RumLang.Analyzer;

public class ExternalEntry
{
    public List<AnalyzerNamespace> Namespaces { get; set; }
    /// <summary>
    /// File local functions that are not part of any namespace or class.
    /// </summary>
    public List<FunctionDeclarationExpression> TopLevelFunctions { get; set; } = new List<FunctionDeclarationExpression>();

    public List<AnalyzerType> TopLevelTypes { get; set; } = new List<AnalyzerType>();
    
    public List<AnalyzerImport> Imports { get; set; } = new();
    public List<AstNode> Nodes { get; set; }

    public ExternalEntry(Rum rum, string source, AnalyzerOptions options)
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
        
        Nodes = root;
        
        Crawl(root, options);
    }
    
    public ExternalEntry(List<AstNode> root, AnalyzerOptions options)
    {
        if (root == null || root.Count == 0)
            throw new ArgumentException("Root nodes cannot be null or empty.", nameof(root));
     
        Namespaces = new List<AnalyzerNamespace>();
        TopLevelFunctions = new List<FunctionDeclarationExpression>();
        TopLevelTypes = new List<AnalyzerType>();
        Imports = new List<AnalyzerImport>();
        
        Nodes = root;
        
        Crawl(root, options);
    }

    private void Crawl(List<AstNode> root, AnalyzerOptions options)
    {
        Namespaces = new List<AnalyzerNamespace>();
        void CrawlNodes (List<AstNode> nodes, AnalyzerNamespace? parentNamespace = null)
        {
            foreach (var node in nodes)
            {
                if (node is ImportExpression ie)
                {
                    Imports.Add(new AnalyzerImport(ie.Target, options));
                }
                else if (node is NamespaceDeclarationExpression nsd)
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
                    else 
                        TopLevelTypes.Add(type);
                }
                else if (node is FunctionDeclarationExpression fde)
                {
                    if (parentNamespace == null)
                        TopLevelFunctions.Add(fde);
                    else
                        parentNamespace.Functions.Add(fde);
                }
            }
        }
        
        // Start crawling from the root nodes
        CrawlNodes(root);
    }
}