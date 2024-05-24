using System.Text.RegularExpressions;

namespace EDExoBioHunt;

public class SystemNameBreakout
{
    private static readonly Regex BreakoutExpression = new(@"^(?<R>.+)\s(?<B>\w\w-\w)\s(?<M>\w)(?<S1>\d+)-(?<S2>\d+)$");

    public SystemNameBreakout(string region, string boxel, string massCode, int majorSeries, int minorSeries)
    {
        Region = region;
        Boxel = boxel;
        MassCode = massCode;
        MajorSeries = majorSeries;
        MinorSeries = minorSeries;
    }

    public readonly string Region;
    public readonly string Boxel;
    public readonly string MassCode;
    public readonly int MajorSeries;
    public readonly int MinorSeries;

    public static SystemNameBreakout? Parse(string systemName)
    {
        var m = BreakoutExpression.Match(systemName);
        if (!m.Success)
            return null;

        var majorSeries = int.Parse(m.Groups["S1"].Value);
        var minorSeries = int.Parse(m.Groups["S2"].Value);
        return new SystemNameBreakout(m.Groups["R"].Value, m.Groups["B"].Value, m.Groups["M"].Value, majorSeries, minorSeries);
    }
}