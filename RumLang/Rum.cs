using System.Text;
using RumLang.Analyzer;
using RumLang.Parser;
using RumLang.Tokenizer;

namespace RumLang;

public record RumResult(RumError Error, string? ErrorMessage = null, string? OutputCode = null);

public class Rum
{
    public readonly string[] LibraryDirectories;

    public Rum(params string[] libraryDirectories)
    {
        this.LibraryDirectories = libraryDirectories;
    }

    // Expose Tokenize
    public (TokenizerError Error, List<Token>? Tokens, string? ErrorString, RumTokenizer tokenizer) Tokenize(string source)
    {
        var tokenizer = new RumTokenizer();
        var result = tokenizer.Tokenize(source);
        return (result.Error, result.Tokens, result.ErrorString, tokenizer);
    }

    // Expose Parse
    public (ParserError Error, List<AstNode>? Root, string? ErrorString, RumParser parser) Parse(List<Token> tokens)
    {
        var parser = new RumParser(tokens);
        var result = parser.Parse();
        return (result.Error, result.Root, result.ErrorString, parser);
    }

    public RumResult Compile(string sourceCode, bool printDebugInfo = false)
    {
        // Tokenization
        var (tokErr, tokens, tokErrMsg, tokenizer) = Tokenize(sourceCode);
        if (tokErr != TokenizerError.Success)
            return new RumResult(RumError.TokenizerError, tokErrMsg);
        if (printDebugInfo)
            Console.WriteLine(tokenizer.GetDebugInfo());

        // Parsing
        var (parseErr, astRoot, parseErrMsg, parser) = Parse(tokens!);
        if (parseErr != ParserError.Success)
            return new RumResult(RumError.ParserError, parseErrMsg);
        if (printDebugInfo)
            Console.WriteLine(parser.GetDebugInfo());

        // Semantic Analysis
        var analyzer = new RumAnalyzer(this, astRoot!);
        var analysisResults = analyzer.Analyze();
        var errors = analysisResults.FindAll(r => r.Type != AnalyzerResultType.Success);
        if (errors.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var r in errors)
                sb.AppendLine($"Error: {r.Message} at Line {r.LineNumber}, Column {r.ColumnNumber}");
            return new RumResult(RumError.AnalysisError, sb.ToString());
        }

        if (printDebugInfo)
        {
            foreach (var r in analysisResults)
                Console.WriteLine($"{r.Type}: {r.Message} at Line {r.LineNumber}, Column {r.ColumnNumber}");
        }

        // Code generation placeholder
        var outputCode = new StringBuilder();
        return new RumResult(RumError.Success, outputCode.ToString());
    }
}
