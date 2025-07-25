using System.Diagnostics;
using QbeGenerator;
using RumCompiler;
using RumLang;

var arguments = new ConsoleUtils(args, new List<IArgument>()
{
    new ConsoleArgument<string>("i", "Input file path", true),
    new ConsoleArgument<string>("o", "Output file path (QBE ir)", true),
    new ConsoleArgument<string>("ob", "Output binary path"),
    new ConsoleFlag("h", "Show help"),
    new ConsoleFlag("d", "Show all debug information"),
    new ConsoleFlag("ar", "Auto run after compilation (only works if compiler is specified & program has entry point)"),
    new ConsoleArgument<string>("compiler", "Compiler name (gcc/clang), if none specified it will just compile to QBE code without generating an executable", false),
    new ConsoleArgument<string>("arch", "QBE architecture (amd64_sysv (default), amd64_apple, arm64, arm64_apple, rv64)", false),
    new ConsoleArgument<string>("l", "Link libraries (comma-seperated list of things that will be passed to GCC/Clang as -l<libname> (for example: \"SDL2,m,dl\"))", false),
});

if (arguments.FlagExists("h"))
{
    Console.WriteLine("Rum Compiler");
    arguments.PrintHelp();
    return;
}

var inputFilePath = arguments.GetArgumentValue<string>("i");
var outputFilePath = arguments.GetArgumentValue<string>("o");

if (string.IsNullOrWhiteSpace(inputFilePath) || string.IsNullOrWhiteSpace(outputFilePath))
{
    Console.WriteLine("Input and output file paths must be specified.");
    return;
}

if (!File.Exists(inputFilePath))
{
    Console.WriteLine($"Input file '{inputFilePath}' does not exist.");
    return;
}

var rum = new Rum("./stdlib"); // Pass in a directory for the standard library.
// You can pass in more directories if needed, e.g. new Rum("./stdlib", "./morelibs");

var sourceCode = File.ReadAllText(inputFilePath);

Stopwatch sw = Stopwatch.StartNew();
var result = rum.Compile(sourceCode, arguments.FlagExists("d")); // returns a (bool Success, string? ErrorMessage, string? OutputCode) where outputCode is QBE code
if (result.Error != RumError.Success)
{
    Console.WriteLine($"Compilation failed: {result.ErrorMessage}");
    return;
}
sw.Stop();
long qbeCompileTime = sw.ElapsedMilliseconds;

if (string.IsNullOrEmpty(result.OutputCode))
{
    Console.WriteLine("Compilation succeeded but no output code was generated.");
    return;
}
Console.WriteLine("Compilation succeeded. Output code generated.");

if (File.Exists(outputFilePath))
{
    Console.WriteLine($"Output file '{outputFilePath}' already exists. Overwriting.");
}

File.WriteAllText(outputFilePath, result.OutputCode ?? string.Empty);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"QBE code generated successfully at {outputFilePath}.");
Console.WriteLine($"QBE compilation time: {qbeCompileTime}ms.");
Console.ResetColor();

if (arguments.GetArgumentValue<string>("ob") != null)
{
    var outputBinaryPath = arguments.GetArgumentValue<string>("ob");
    if (string.IsNullOrWhiteSpace(outputBinaryPath))
    {
        Console.WriteLine("Output binary path must be specified.");
        return;
    }

    if (string.IsNullOrWhiteSpace(arguments.GetArgumentValue<string>("compiler")))
    {
        Console.WriteLine("No compiler specified, only generating QBE code.");
        return;
    }

    var compiler = arguments.GetArgumentValue<string>("compiler")!.ToLower();
    if (compiler != "gcc" && compiler != "clang")
    {
        Console.WriteLine($"Unsupported compiler \"{compiler}\". Supported compilers are \"gcc\" and \"clang\".");
        return;
    }

    string arch = arguments.GetArgumentValue<string>("arch")?.ToLower() ?? "amd64_sysv";
    
    var qbeFile = outputFilePath;
    var tempAsmFile = Path.GetTempFileName() + ".s";
    sw = Stopwatch.StartNew();
    QbeCompiler.Compile(File.ReadAllText(qbeFile), out var assemblySource, out var qbeError, arch);
    if (!string.IsNullOrEmpty(qbeError))
    {
        Console.WriteLine($"QBE compilation failed: {qbeError}");
        return;
    }
    sw.Stop();
    long qbeToAsmCompileTime = sw.ElapsedMilliseconds;
    
    File.WriteAllText(tempAsmFile, assemblySource);
    Console.WriteLine($"QBE code generated successfully at {qbeFile}.");
    Console.WriteLine($"Assembly code generated at {tempAsmFile}.");
    
    var linkLibraries = arguments.GetArgumentValue<string>("l");
    string[] linkFlags = Array.Empty<string>();
    if (!string.IsNullOrWhiteSpace(linkLibraries))
    {
        var libs = linkLibraries.Split(',').Select(lib => $"-l{lib.Trim()}").ToArray();
        Console.WriteLine($"Linking libraries: {string.Join(", ", libs)}");
        linkFlags = libs;
    }
    
    sw = Stopwatch.StartNew();
    var exitCode = Rum.RunProcess(compiler, $"-o {outputBinaryPath} {tempAsmFile} -O2 -lm {string.Join(" ", linkFlags)}");
    if (exitCode != 0)
    {
        Console.WriteLine($"Compilation with {compiler} failed with exit code {exitCode}.");
        return;
    }
    sw.Stop();
    long asmToBinaryCompileTime = sw.ElapsedMilliseconds;


    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Binary compiled successfully at {outputBinaryPath}.");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"QBE to Assembly compile time: {qbeToAsmCompileTime}ms.");
    Console.WriteLine($"Assembly to Binary compile time: {asmToBinaryCompileTime}ms.");
    Console.WriteLine($"Total compile time: {qbeCompileTime + qbeToAsmCompileTime + asmToBinaryCompileTime}ms.");
    Console.ResetColor();


    if (arguments.FlagExists("ar") && result.EntryPoint != null && File.Exists(outputBinaryPath))
    {
        Console.WriteLine("Running the compiled binary...");
        var runExitCode = Rum.RunProcess(outputBinaryPath, "");
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Program finished executing with exit code {runExitCode}.");
        Console.ResetColor();
    }
}
else
{
    Console.WriteLine("No output binary path specified, only generating QBE code.");
}