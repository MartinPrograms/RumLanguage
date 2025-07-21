using System.Text;

namespace RumLang;

public record RumResult(RumError Error, string? ErrorMessage = null, string? OutputCode = null);

public class Rum
{
    public RumResult Compile(string sourceCode)
    {
        StringBuilder outputCode = new StringBuilder();
        return new RumResult(RumError.Success, outputCode.ToString());
    }
}