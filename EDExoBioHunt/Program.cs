using Octurnion.Common.Utils;
using Octurnion.Common.Utils.CommandLine;
using Octurnion.EliteDangerousUtils.EDSM.Client;

namespace EDExoBioHunt;

internal class Program
{
    private const ConsoleColor InfoColor = ConsoleColor.White;
    private const ConsoleColor WarningColor = ConsoleColor.Yellow;
    private const ConsoleColor ErrorColor = ConsoleColor.Red;
    
    private static readonly object ConsoleLock = new();
    private static readonly EdsmClient EdsmClient = new();

    static void Main(string[] args)
    {
        Options options;

        try
        {
            options = Options.Parse();
        }
        catch (CommandLineException e)
        {
            Error(e.Message);
            return;
        }
        catch (Exception e)
        {
            Error($"Unhandled exception parsing command line: {e.Message}");
            return;
        }

        switch (options.Mode)
        {
            case Options.ModeEnum.SystemFinder:
                RunSystemFinder(options.SystemFinderOptions);
                break;

            case Options.ModeEnum.HuntList:
                RunHuntList(options.HuntListOptions);
                break;

            default:
                Error($"Unsupported mode {options.Mode}");
                break;
        }
    }

    private static void RunSystemFinder(SystemFinderOptions? options)
    {
        var finder = new ExoBioSystemFinder(Message);
        var massCodes = options!.MassCodes.Select(c => $"{c}").ToArray();
        finder.FindMassCodeSystemsInCube(options.CentralSystemName, options.Size, massCodes);
    }

    private static void RunHuntList(HuntListOptions? options)
    {
        var finder = new ExoBioSystemFinder(Message);
        finder.BuildHuntList(options!.File);
    }
    
    private static void Write(string message)
    {
        lock (ConsoleLock)
        {
            Console.Out.WriteLine(message);
        }
    }

    private static void Message(string message, StatusMessageSeverity severity)
    {
        lock (ConsoleLock)
        {
            var currentColor = Console.ForegroundColor;

            switch (severity)
            {
                case StatusMessageSeverity.Warning:
                    Console.ForegroundColor = WarningColor;
                    break;
                    
                case StatusMessageSeverity.Error:
                    Console.ForegroundColor = ErrorColor;
                    break;

                default:
                    Console.ForegroundColor = InfoColor;
                    break;
            }

            Console.Error.WriteLine(message);
            Console.ForegroundColor = currentColor;
        }
    }

    private static void Info(string message) => Message(message, StatusMessageSeverity.Normal);
    
    private static void Warning(string message) => Message(message, StatusMessageSeverity.Warning);
    
    private static void Error(string message) => Message(message, StatusMessageSeverity.Error);
}