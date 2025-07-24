namespace RumLang.Analyzer;

public class AnalyzerImport
{
    public string Name { get; } // e.g., "rum.lang.foo.bar" or for c headers "c.stdio" (without ".h", turns into "stdio.h", sometimes the .h is omitted, if it exists without.)
    // As for c headers in directories, you'd just add more dots. "c.SDL3.SDL" turns into "SDL3/SDL.h"
    
    // If the import is a C header, this will be true.
    public bool IsCHeader { get; }
    
    public string? CHeaderName { get; } // e.g., "stdio.h" or "SDL3/SDL.h"
    
    public AnalyzerImport(string name, AnalyzerOptions options)
    {
        Name = name;
        
        // Process the name to determine the C header name
        if (name.StartsWith(options.CPrefix))
        {
            IsCHeader = true;
            name = name.Substring(options.CPrefix.Length);
            name = name.Replace('.', '/');
            CHeaderName = $"{name}.h";
        }
        else
        {
            IsCHeader = false;
            CHeaderName = null;
        }
    }
}