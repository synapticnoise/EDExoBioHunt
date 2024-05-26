using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;
using Octurnion.EliteDangerousUtils.Journal;
using Octurnion.EliteDangerousUtils.Journal.Entries;

namespace EDExoBioHunt;

public class ExoBioSystemFinder
{
    private readonly StatusUpdateDelegate? _statusUpdateDelegate;
    private readonly EdsmClient _edsmClient = new();
    private readonly SystemCache _systemCache = new();

    public ExoBioSystemFinder(StatusUpdateDelegate? statusUpdateDelegate)
    {
        _statusUpdateDelegate = statusUpdateDelegate;
    }

    public void FindMassCodeSystemsInCube(string centralSystemName, int size, string[] massCodes)
    {
        var massCodeSet = new HashSet<string>(massCodes, StringComparer.InvariantCultureIgnoreCase);
        _systemCache.CacheSystems([centralSystemName]);
        if (!_systemCache.SystemsByName.TryGetValue(centralSystemName, out var centralSystem))
        {
            Error($"Could not find central system {centralSystemName}");
            return;
        }

        if (centralSystem.Coordinates == null)
        {
            Error($"Central system {centralSystemName} does not have coordinates.");
            return;
        }

        _systemCache.CacheSystemsInCube(centralSystem.Coordinates, size);

        var systemsInRange = _systemCache.GetSystemsInCuboid(new Cuboid(centralSystem.Coordinates, size)).ToArray();

        Info($"Selected {systemsInRange.Length} systems in range.");

        var filteredSystemsByPrefix = CategorizeSystems(systemsInRange)
            .Where(t => massCodeSet.Contains(t.breakout.MassCode))
            .GroupBy(t => GetBreakoutPrefix(t.breakout))
            .OrderByDescending(g => g.Count());

        Console.WriteLine("Prefix\tCounts\tStar Types\tPlanet Types\tAtmosphere Types");

        foreach (var gPrefix in filteredSystemsByPrefix)
        {

            var stDict = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            var ptDict = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            var atDict = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var body in gPrefix.SelectMany(t => t.system.Bodies ?? []))
            {
                switch (body)
                {
                    case EdsmStar star:
                        var starClass = star.GetClass();
                        if (starClass != null)
                        {
                            if (!stDict.TryAdd(starClass, 1))
                                stDict[starClass]++;
                        }

                        break;

                    case EdsmPlanet planet:
                        if (planet.IsLandable)
                        {
                            if (!string.IsNullOrWhiteSpace(planet.AtmosphereType) && !planet.AtmosphereType.Equals("no atmosphere", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (!atDict.TryAdd(planet.AtmosphereType, 1))
                                    atDict[planet.AtmosphereType]++;
                            }

                            if (!string.IsNullOrWhiteSpace(planet.SubType))
                            {
                                if (!ptDict.TryAdd(planet.SubType, 1))
                                    ptDict[planet.SubType]++;
                            }
                        }

                        break;
                }
            }

            var min = gPrefix.Min(t => t.breakout.MinorSeries);
            var max = gPrefix.Max(t => t.breakout.MinorSeries);
            var count = gPrefix.Count();
            var countCol = $"{count} ({min} - {max})";

            var stElements = stDict
                .OrderByDescending(t => t.Value)
                .Select(t => $"{t.Key} ({t.Value})");

            var ptElements = ptDict
                .OrderByDescending(t => t.Value)
                .Select(t => $"{t.Key} ({t.Value})");

            var atElements = atDict
                .OrderByDescending(t => t.Value)
                .Select(t => $"{t.Key} ({t.Value})");

            var stCol = string.Join(", ", stElements);
            var ptCol = string.Join(", ", ptElements);
            var atCol = string.Join(", ", atElements);

            Console.WriteLine($"{gPrefix.Key}\t{countCol}\t{stCol}\t{ptCol}\t{atCol}");
        }
    }

    public void BuildHuntList(FileInfo systemNameFile)
    {
        var systemNames = LoadSystemFile(systemNameFile).ToArray();
        Info($"Loaded {systemNames.Length} systems.");
        _systemCache.CacheSystems(systemNames);
        var visitedSystems = GetVisitedSystemNames();
        var nonVisitedSystems = systemNames.Where(s => !visitedSystems.Contains(s)).ToArray();
        Info($"Culled {systemNames.Length - nonVisitedSystems.Length} visited systems.");
        
        //var edsmSystems = GetSystemsFromEdsm(systemNames).ToDictionary(s => s.Name!);
        var navSystems = GetNavRouteSystems();

        Console.WriteLine("System\tX\tY\tZ\tClass\tBodies");

        foreach (var name in nonVisitedSystems)
        {
            if (!navSystems.TryGetValue(name, out var navSystem))
            {
                Warning($"System {name} not found in nav route history");
            }
            
            var coordinates = navSystem.coordinates;
            var bodies = string.Empty;

            if (_systemCache.SystemsByName.TryGetValue(name, out var edsmSystem))
            {
                if (edsmSystem.Coordinates != null)
                    coordinates = new Coordinates(edsmSystem.Coordinates.X, edsmSystem.Coordinates.Y, edsmSystem.Coordinates.Z);

                if (edsmSystem.Bodies?.Length > 0)
                {
                    var sc = edsmSystem.Bodies.OfType<EdsmStar>().Count();
                    var pc = edsmSystem.Bodies.OfType<EdsmPlanet>().Count();

                    bodies = string.Join(", ", Elements());

                    IEnumerable<string> Elements()
                    {
                        if (sc > 0) yield return $"S: {sc}";
                        if (pc > 0) yield return $"P: {pc}";
                    }
                }

                //var parameters = new EdsmGetSystemParameters
                //{
                //    SystemName = name
                //};

                //Info($"Fetching bodies for {name} from EDSM.");
                //_edsmClient.Throttle();
                //var json = _edsmClient.GetSystemBodies(parameters);

                //if (string.IsNullOrEmpty(json) || json == "{}")
                //{
                //    Warning($"Empty response from EDSM for system {name}");
                //}
                //else
                //{
                //    var withBodies = EdsmSystem.ParseWithBodies(json);
                //    if (withBodies.Bodies?.Length > 0)
                //    {
                //        var sc = withBodies.Bodies.OfType<EdsmStar>().Count();
                //        var pc = withBodies.Bodies.OfType<EdsmPlanet>().Count();

                //        bodies = string.Join(", ", Elements());

                //        IEnumerable<string> Elements()
                //        {
                //            if (sc > 0) yield return $"S: {sc}";
                //            if (pc > 0) yield return $"P: {pc}";
                //        }
                //    }
                //}
            }

            Console.WriteLine($"{name}\t{coordinates.X}\t{coordinates.Y}\t{coordinates.Z}\t{navSystem.starClass}\t{bodies}");
        }
    }

    private IEnumerable<string> LoadSystemFile(FileInfo file)
    {
        using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            yield return line;
        }
    }

    private IDictionary<string, (Coordinates coordinates, string starClass)> GetNavRouteSystems()
    {
        Dictionary<string, (Coordinates coordinates, string starClass)> dict = new(StringComparer.InvariantCultureIgnoreCase);

        foreach (var navRoute in NavRoute.GetRouteHistory())
        {
            foreach (var entry in navRoute.Route ?? [])
                dict.TryAdd(entry.StarSystem!, (new Coordinates(entry.X, entry.Y, entry.Z), entry.StarClass ?? string.Empty));
        }

        return dict;
    }

    //public static IEnumerable<NavRoute> GetRouteHistory()
    //{
    //    var file = GameDataHelperSingleton.Instance.NavRouteHistoryFile;

    //    FileStream? stream = null;
    //    StreamReader? reader = null;

    //    try
    //    {
    //        stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
    //        reader = new StreamReader(stream);
    //    }
    //    catch (Exception e)
    //    {
    //        reader?.Dispose();
    //        stream?.Dispose();
    //        throw new JournalFileException($"Failed to open navigation route history file \"{file.FullName}\"", e);
    //    }

    //    while (true)
    //    {
    //        string? line;

    //        try
    //        {
    //            line = reader.ReadLine();
    //            if (line == null)
    //                break;

    //            if (string.IsNullOrWhiteSpace(line))
    //                continue;
    //        }
    //        catch (Exception e)
    //        {
    //            throw new JournalFileException($"Failed to read navigation route history file \"{file.FullName}\"", e);
    //        }

    //        NavRoute? route;

    //        try
    //        {
    //            route = JsonSerializer.Deserialize<NavRoute>(line);
    //        }
    //        catch (Exception e)
    //        {
    //            throw new JournalFileException($"Failed to parse navigation route history entry.", e);
    //        }

    //        if (route == null)
    //            throw new JournalFileException($"Failed to parse navigation route history entry deserialized as null.");

    //        yield return route;
    //    }

    //    reader.Dispose();
    //    stream.Dispose();
    //}

    private IEnumerable<EdsmSystemSummary> GetSystemsFromEdsm(string[] systemNames)
    {
        const int systemsPerCall = 10;
        var blocks = systemNames.Partition(systemsPerCall).ToArray();

        foreach (var (index, block) in blocks.WithIndex())
        {
            Info($"Fetching system information from EDSM ({index + 1} of {blocks.Length})");

            var parameters = new EdsmGetMultipleSystemsParameters
            {
                SystemNames = block.ToArray(),
                ShowCoordinates = true,
                ShowPrimaryStar = true
            };

            _edsmClient.Throttle();
            var json = _edsmClient.GetSystems(parameters);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Warning($"Empty response from EDSM.");
                continue;
            }

            var systems = EdsmSystemSummary.ParseArray(json);

            foreach (var system in systems)
                yield return system;
        }
    }

    private HashSet<string> GetVisitedSystemNames()
    {
        HashSet<string> set = new(StringComparer.InvariantCultureIgnoreCase);

        foreach (var jump in JournalFile.GetJournalEntries().Where(e => e.Entry != null).Select(e => e.Entry!).OfType<FsdJumpJournalEntry>())
        {
            if(!string.IsNullOrEmpty(jump.StarSystem))
                set.Add(jump.StarSystem);
        }

        return set;
    }


    private static string GetBreakoutPrefix(SystemNameBreakout breakout) =>
        $"{breakout.Region} {breakout.Boxel} {breakout.MassCode}{breakout.MajorSeries}";

    private static IEnumerable<(EdsmSystem system, SystemNameBreakout breakout)> CategorizeSystems(IEnumerable<EdsmSystem> systems)
    {
        foreach (var system in systems)
        {
            var breakout = SystemNameBreakout.Parse(system.Name!);
            if (breakout == null) continue;
            yield return (system, breakout);
        }
    }

    private void Info(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Normal);

    private void Warning(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Warning);

    private void Error(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Error);
    
}