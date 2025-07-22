using System.Text;
using RumLang.Parser;
using RumLang.Tokenizer;

namespace RumLang;

public record RumResult(RumError Error, string? ErrorMessage = null, string? OutputCode = null);

public class Rum(params string[] libraryDirectories)
{
    private readonly string[] _libraryDirectories = libraryDirectories;
    
    public RumResult Compile(string sourceCode, bool printDebugInfo = false)
    {
        RumTokenizer tokenizer = new();
        var tokenizerResult = tokenizer.Tokenize(sourceCode);

        if (tokenizerResult.Error != TokenizerError.Success)
            return new RumResult(RumError.TokenizerError, tokenizerResult.ErrorString, null);

        var tokenizerDebugInfo = tokenizer.GetDebugInfo();
        if (printDebugInfo)
            Console.WriteLine(tokenizerDebugInfo);

        RumParser parser = new(tokenizerResult.Tokens!);
        var parserResult = parser.Parse();
        if (parserResult.Error != ParserError.Success)
            return new RumResult(RumError.ParserError, parserResult.ErrorString, null);

        var parserDebugInfo = parser.GetDebugInfo();
        if (printDebugInfo)
            Console.WriteLine(parserDebugInfo);
        
        StringBuilder outputCode = new StringBuilder();
        return new RumResult(RumError.Success, outputCode.ToString());
    }
}