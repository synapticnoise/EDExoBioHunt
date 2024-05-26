using Octurnion.Common.Utils.CommandLine;

namespace EDExoBioHunt;

public static class CommandLineConstants
{
    public const string ModeArgument = "mode";

    public const string CentralSystemNameArgument = "system";
    public const string SizeArgument = "size";
    public const string MassCodesArgument = "mc";

    public const string FileArgument = "file";
}

public class Options
{
    public enum ModeEnum
    {
        SystemFinder,
        HuntList,
        FindingsAnalyser
    }

    public ModeEnum Mode { get; init; }

    public SystemFinderOptions? SystemFinderOptions { get; init; }

    public HuntListOptions? HuntListOptions { get; init; }


    public static Options Parse()
    {
        var args = CommandLineHelper.ParseCommandLineAdvanced(Environment.CommandLine);

        ModeEnum? mode = null;
        args.GetEnumArgument<ModeEnum>(CommandLineConstants.ModeArgument, v => mode=v);

        if (mode == null)
            throw new MissingCommandLineArgumentException(CommandLineConstants.ModeArgument);

        switch (mode)
        {
            case ModeEnum.SystemFinder:
                var sfo = EDExoBioHunt.SystemFinderOptions.Parse(args);
                return new Options { Mode = mode.Value, SystemFinderOptions = sfo};

            case ModeEnum.HuntList:
                var hlo = EDExoBioHunt.HuntListOptions.Parse(args);
                return new Options { Mode = mode.Value, HuntListOptions = hlo };

            //case ModeEnum.FindingsAnalyser:
            //    break;

            default:
                throw new CommandLineArgumentException(CommandLineConstants.ModeArgument, a => $"Unsupported value {mode} for argument -{a}.");
        }
    }
}

public class SystemFinderOptions
{
    public SystemFinderOptions(string centralSystemName, int size, string massCodes)
    {
        CentralSystemName = centralSystemName;
        Size = size;
        MassCodes = massCodes;
    }

    public string CentralSystemName { get; init; }

    public int Size { get; init; }

    public string MassCodes { get; init; }

    public static SystemFinderOptions Parse(CommandLineArgumentDictionary args)
    {
        string? centralSystemName=null;
        args.GetStringArgument(CommandLineConstants.CentralSystemNameArgument, v => centralSystemName = v);
        if (centralSystemName == null)
            throw new MissingCommandLineArgumentException(CommandLineConstants.CentralSystemNameArgument);

        int? size = null;
        args.GetIntArgument(CommandLineConstants.SizeArgument, v => size=v);
        if (size == null)
            throw new MissingCommandLineArgumentException(CommandLineConstants.SizeArgument);

        string? massCodes = null;
        args.GetStringArgument(CommandLineConstants.MassCodesArgument, v => massCodes = v);
        if (massCodes == null)
            throw new MissingCommandLineArgumentException(CommandLineConstants.MassCodesArgument);

        return new SystemFinderOptions(centralSystemName!, size.Value, massCodes!);
    }
}

public class HuntListOptions
{
    public HuntListOptions(FileInfo file)
    {
        File = file;
    }

    public FileInfo File { get; init; }

    public static HuntListOptions Parse(CommandLineArgumentDictionary args)
    {
        FileInfo? file=null;
        args.GetFileArgument(CommandLineConstants.FileArgument, v => file =v, true);
        if (file == null)
            throw new MissingCommandLineArgumentException(CommandLineConstants.FileArgument);

        return new HuntListOptions(file);
    }
}

