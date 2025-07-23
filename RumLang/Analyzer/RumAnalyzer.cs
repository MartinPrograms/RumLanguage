using System.Collections;
using System.Diagnostics;
using RumLang.Parser;

namespace RumLang.Analyzer;

public enum AnalyzerResultType
{
    Success,
    Error,
    Warning
}

public record AnalyzerResult(AnalyzerResultType Type, string Message, int LineNumber = -1, int ColumnNumber = -1);

public class RumAnalyzer
{
    private readonly List<AnalyzerResult> _results = new();

    public IReadOnlyList<AnalyzerResult> Results => _results;

    private readonly List<AstNode> _nodes;
    private long _milliseconds = 0;
    private Rum _rum;
    
    private Dictionary<string, DependencyEntry> _dependencyMap = new();
    
    public RumAnalyzer(Rum rum, List<AstNode> Nodes)
    {
        _nodes = Nodes;
        _rum = rum;
        
        // Rum here will be used to access the library directories and other configurations if needed.
        Stopwatch sw = Stopwatch.StartNew();
        
        // Load in all potential files from rum.LibraryDirectories
        var directories = rum.LibraryDirectories;
        foreach (var dir in directories)
        {
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.rum", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var entry = new DependencyEntry(rum, file);
                    foreach (var ns in entry.ExternalEntry.Namespaces)
                    {
                        var str = ns.GetFullName();
                        if (!_dependencyMap.ContainsKey(str))
                        {
                            _dependencyMap[str] = entry;
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
                                $"Namespace '{str}' defined in file: {file}"));
                        }
                        else
                        {
                            // If the namespace already exists, we can log a warning or handle it as needed.
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Warning, 
                                $"Namespace '{str}' is already defined in another file: {_dependencyMap[str].FilePath}"));
                        }
                    }
                }
            }
        }
        sw.Stop();
        _milliseconds = sw.ElapsedMilliseconds;
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
            $"Dependency scan completed in {_milliseconds} milliseconds. Found {_dependencyMap.Count} unique namespaces."));
    }

    public List<AnalyzerResult> Analyze()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        // Do the processing
        
        stopwatch.Stop();
        _milliseconds = stopwatch.ElapsedMilliseconds;
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success, "Analysis finished in " + _milliseconds + " milliseconds."));
        
        return _results;
    }
}