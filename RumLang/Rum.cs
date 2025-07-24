using System.Diagnostics;
using System.Text;
using QbeGenerator;
using RumLang.Analyzer;
using RumLang.CodeGen;
using RumLang.Parser;
using RumLang.Tokenizer;

namespace RumLang;

public record RumResult(RumError Error, string? ErrorMessage = null, string? OutputCode = null, string? EntryPoint = null);

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

    public RumResult Compile(string sourceCode, bool printDebugInfo = false, string entryPoint = "main")
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

        if (printDebugInfo)
        {
            foreach (var r in analysisResults)
                Console.WriteLine($"{StringHelpers.AlignTo(r.Type.ToString(),12, true)}: {r.Message}");
        }
        
        if (analysisResults.Any(r => r.Type == AnalyzerResultType.Error))
        {
            var errorMessages = analysisResults
                .Where(r => r.Type == AnalyzerResultType.Error)
                .Select(r => $"{r.Message}")
                .ToList();
            return new RumResult(RumError.AnalysisError, $"\n{string.Join("\n", errorMessages)}");
        }

        if (!string.IsNullOrEmpty(analyzer.EntryPoint))
        {
            if (analyzer.EntryPoint != entryPoint)
            {
                return new RumResult(RumError.AnalysisError, $"Entry point mismatch: expected '{entryPoint}', found '{analyzer.EntryPoint}'");
            }
        }

        // Code generation placeholder
        var codegen = new RumCodeGen(this, astRoot!, analyzer);
        var (outputCode, codegenError, ms) = codegen.GenerateCode();
        
        int lineCounter = 1;
        if (printDebugInfo)
        {
            foreach (var line in outputCode.Split('\n'))
            {
                Console.WriteLine($"{lineCounter++}:\t{line}");
            }
            Console.WriteLine($"Code generation took {ms} ms");
        }
        
        if (codegenError != CodeGenError.Success)
        {
            return new RumResult(RumError.CodeGenError, codegenError.ToString());
        }
        
        return new RumResult(RumError.Success, "No error!", outputCode, analyzer.EntryPoint);
    }
    
    public static int RunProcess(string filename, string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (!string.IsNullOrEmpty(stdout))
            Console.WriteLine(stdout);
        if (!string.IsNullOrEmpty(stderr))
            Console.Error.WriteLine(stderr);

        return process.ExitCode;
    }
}
