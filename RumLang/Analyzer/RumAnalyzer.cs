using System.Collections;
using System.Diagnostics;
using RumLang.Parser;
using RumLang.Parser.Definitions;

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
    
    /// <summary>
    /// A list of dependency entries, where each entry corresponds to a file that defines one or more namespaces.
    /// </summary>
    private Dictionary<string, List<DependencyEntry>> _dependencyMap = new();
    private Dictionary<string, FunctionDeclarationExpression> _functionMap = new(); // e.g., "rum.io.printf" => FunctionDeclarationExpression (which can be used to call the function)
    private Dictionary<string, AnalyzerType> _typeMap = new(); // e.g., "rum.text.string" => AnalyzerType (which can be used to create instances of the type)
    
    public RumAnalyzer(Rum rum, List<AstNode> nodes)
    {
        _nodes = nodes;
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
                            _dependencyMap[str] = new List<DependencyEntry>();
                        }
                        _dependencyMap[str].Add(entry);
                        
                        
                        _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
                            $"Namespace '{str}' defined in file: {file}"));
                    }
                }
            }
        }
        sw.Stop();
        _milliseconds = sw.ElapsedMilliseconds;
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
            $"Dependency scan completed in {_milliseconds} milliseconds. Found {_dependencyMap.Count} unique namespaces."));
        
        // Crawl the _dependencyMap to populate _functionMap and _typeMap
        sw.Restart();
        void RecursiveCrawl(List<DependencyEntry> entries)
        {
            foreach (var entry in entries)
            {
                foreach (var ns in entry.ExternalEntry.Namespaces)
                {
                    // Add functions to the function map
                    foreach (var func in ns.Functions)
                    {
                        var fullName = $"{ns.GetFullName()}.{func.FunctionName}";
                        if (!_functionMap.ContainsKey(fullName))
                        {
                            _functionMap[fullName] = func;
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
                                $"Function '{fullName}' added to function map."));
                        }
                    }

                    // Add types to the type map
                    foreach (var type in ns.Types)
                    {
                        var fullName = $"{ns.GetFullName()}.{type.Name}";
                        if (!_typeMap.ContainsKey(fullName))
                        {
                            _typeMap[fullName] = type;
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
                                $"Type '{fullName}' added to type map."));
                        }
                    }
                    
                    foreach (var subNs in ns.SubNamespaces)
                    {
                        // Recursively crawl sub-namespaces
                        var subNsFullName = $"{ns.GetFullName()}.{subNs.Name}";
                        if (_dependencyMap.ContainsKey(subNsFullName))
                        {
                            RecursiveCrawl(_dependencyMap[subNsFullName]);
                        }
                        else
                        {
                            _results.Add(new AnalyzerResult(AnalyzerResultType.Warning, 
                                $"Sub-namespace '{subNsFullName}' not found in dependency map."));
                        }
                    }
                }
            }
        }
        
        RecursiveCrawl(_dependencyMap.Values.SelectMany(x => x).ToList());
        sw.Stop();
        _milliseconds = sw.ElapsedMilliseconds;
        _results.Add(new AnalyzerResult(AnalyzerResultType.Success, 
            $"Crawling dependencies completed in {_milliseconds} milliseconds. Found {_functionMap.Count} functions and {_typeMap.Count} types."));
        
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