namespace RumCompiler;

public interface IArgument
{
    public string Name { get; }
    public string Description { get; }
    public bool IsRequired { get; }
}

public record ConsoleArgument<T>(string Name, string Description, bool IsRequired = false)
    : IArgument
{
    public T? Value { get; set; } = default!;
}

public record ConsoleFlag(string Name, string Description)
    : IArgument
{
    public bool IsPresent { get; set; } = false;
    public bool IsRequired => false; // Flags are not required by default
}

public class ConsoleUtils
{
    public List<IArgument> Arguments { get; private set; }
    
    public ConsoleUtils(string[] args, List<IArgument> arguments)
    {
        Arguments = arguments;
        Load(args);
    }

    private void Load(string[] args)
    {
        // -name value
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("-"))
            {
                var name = arg.TrimStart('-');
                var value = i + 1 < args.Length ? args[i + 1] : null;
                var argument = Arguments.FirstOrDefault(a => a.Name == name);

                if (argument is ConsoleFlag)
                    ((ConsoleFlag)argument).IsPresent = true;
                else
                {
                    // Get the type
                    var type = argument.GetType().GetGenericArguments().FirstOrDefault();
                    if (type != null && value != null)
                    {
                        var convertedValue = Convert.ChangeType(value, type);
                        if (argument is ConsoleArgument<object> consoleArg)
                        {
                            consoleArg.Value = convertedValue;
                        }
                        else if (argument is ConsoleArgument<string> stringArg)
                        {
                            stringArg.Value = value;
                        }
                        else if (argument is ConsoleArgument<int> intArg && int.TryParse(value, out var intValue))
                        {
                            intArg.Value = intValue;
                        }
                        else if (argument is ConsoleArgument<bool> boolArg && bool.TryParse(value, out var boolValue))
                        {
                            boolArg.Value = boolValue;
                        }
                        else
                        {
                            throw new ArgumentException($"Argument '{name}' has an unsupported type or value.");
                        }
                    }
                }
            }
        }
        
        // Check required arguments
        foreach (var argument in Arguments)
        {
            if (argument is ConsoleArgument<object> consoleArg && consoleArg.IsRequired && consoleArg.Value == null)
            {
                throw new ArgumentException($"Required argument '{consoleArg.Name}' is missing.");
            }
        }
    }

    public bool FlagExists(string s)
    {
        var flag = Arguments.OfType<ConsoleFlag>().FirstOrDefault(f => f.Name == s);
        return flag != null && flag.IsPresent;
    }
    
    public T? GetArgumentValue<T>(string name)
    {
        var argument = Arguments.OfType<ConsoleArgument<T>>().FirstOrDefault(a => a.Name == name);
        if (argument == null)
            throw new ArgumentException($"Argument '{name}' not found.");
        return argument.Value;
    }

    public void PrintHelp()
    {
        Console.WriteLine("Available arguments:");
        foreach (var arg in Arguments)
        {
            if (arg is ConsoleFlag consoleFlag)
            {
                Console.WriteLine($"- {consoleFlag.Name}: {consoleFlag.Description}");
            }
            else
            {
                Console.WriteLine($"- {arg.Name}: {arg.Description} (Required: {arg.IsRequired})");
            }
        }
    }
}