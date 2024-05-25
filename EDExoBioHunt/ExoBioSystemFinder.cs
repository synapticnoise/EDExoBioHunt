using System.Text.Json;
using MemoryPack;
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

    public ExoBioSystemFinder(StatusUpdateDelegate? statusUpdateDelegate)
    {
        _statusUpdateDelegate = statusUpdateDelegate;
    }

    public void FindMassCodeSystemsInSphere(string centralSystemName, int radius, string[] massCodes)
    {
        var massCodeSet = new HashSet<string>(massCodes, StringComparer.InvariantCultureIgnoreCase);
        CacheSystemsInSphere(centralSystemName, radius);
        var systems = LoadSystems().ToDictionary(s => s.Name!);
        Info($"Loaded {systems.Count} systems.");
        var noCoordinates = systems.Values.Count(s => s.Coordinates == null);
        if (noCoordinates > 0)
        {
            Warning($"{noCoordinates} systems have no coordinates.");
        }

        if (!systems.TryGetValue(centralSystemName, out var centralSystem))
        {
            Error($"Could not find central system {centralSystemName}");
            return;
        }

        if (centralSystem.Coordinates == null)
        {
            Error($"Central system has no coordinates.");
            return;
        }

        var centerCoordinates = new SimpleCoordinates(centralSystem.Coordinates);
        var systemsInRange = systems.Values.Where(s => s.Coordinates != null && centerCoordinates.Distance(new SimpleCoordinates(s.Coordinates)) <= radius).ToArray();
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
        var visitedSystems = GetVisitedSystemNames();
        var nonVisitedSystems = systemNames.Where(s => !visitedSystems.Contains(s)).ToArray();
        Info($"Culled {systemNames.Length - nonVisitedSystems.Length} visited systems.");

        var edsmSystems = GetSystemsFromEdsm(systemNames).ToDictionary(s => s.Name!);
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

            if (edsmSystems.TryGetValue(name, out var edsmSystem))
            {
                if (edsmSystem.Coordinates != null)
                    coordinates = new SimpleCoordinates(edsmSystem.Coordinates);

                var parameters = new EdsmGetSystemParameters
                {
                    SystemName = name
                };

                Info($"Fetching bodies for {name} from EDSM.");
                _edsmClient.Throttle();
                var json = _edsmClient.GetSystemBodies(parameters);

                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    Warning($"Empty response from EDSM for system {name}");
                }
                else
                {
                    var withBodies = EdsmSystem.ParseWithBodies(json);
                    if (withBodies.Bodies?.Length > 0)
                    {
                        var sc = withBodies.Bodies.OfType<EdsmStar>().Count();
                        var pc = withBodies.Bodies.OfType<EdsmPlanet>().Count();

                        bodies = string.Join(", ", Elements());

                        IEnumerable<string> Elements()
                        {
                            if (sc > 0) yield return $"S: {sc}";
                            if (pc > 0) yield return $"P: {pc}";
                        }
                    }
                }
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

    private IDictionary<string, (SimpleCoordinates coordinates, string starClass)> GetNavRouteSystems()
    {
        Dictionary<string, (SimpleCoordinates coordinates, string starClass)> dict = new(StringComparer.InvariantCultureIgnoreCase);

        foreach (var navRoute in GetRouteHistory())
        {
            foreach (var entry in navRoute.Route ?? [])
                dict.TryAdd(entry.StarSystem!, (new SimpleCoordinates(entry.X, entry.Y, entry.Z), entry.StarClass ?? string.Empty));
        }

        return dict;
    }

    public static IEnumerable<NavRoute> GetRouteHistory()
    {
        var file = GameDataHelperSingleton.Instance.NavRouteHistoryFile;

        FileStream? stream = null;
        StreamReader? reader = null;

        try
        {
            stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new StreamReader(stream);
        }
        catch (Exception e)
        {
            reader?.Dispose();
            stream?.Dispose();
            throw new JournalFileException($"Failed to open navigation route history file \"{file.FullName}\"", e);
        }

        while (true)
        {
            string? line;

            try
            {
                line = reader.ReadLine();
                if (line == null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                    continue;
            }
            catch (Exception e)
            {
                throw new JournalFileException($"Failed to read navigation route history file \"{file.FullName}\"", e);
            }

            NavRoute? route;

            try
            {
                route = JsonSerializer.Deserialize<NavRoute>(line);
            }
            catch (Exception e)
            {
                throw new JournalFileException($"Failed to parse navigation route history entry.", e);
            }

            if (route == null)
                throw new JournalFileException($"Failed to parse navigation route history entry deserialized as null.");

            yield return route;
        }

        reader.Dispose();
        stream.Dispose();
    }

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

    private void CacheSystemsInSphere(string centralSystemName, int radius)
    {
        var systems = LoadSystems();
        Info($"Loaded {systems.Length} systems from cache.");
        Info("Fetching systems in sphere from EDSM.");

        var systemsInSphere = FetchSystemsInSphere(centralSystemName, radius).ToDictionary(s => s.Name!);
        Info($"Fetched {systemsInSphere.Count} system summaries from EDSM.");

        var systemNames = new HashSet<string>(systems.Select(s => s.Name!), StringComparer.InvariantCultureIgnoreCase);
        var nonCachedSystemNames = systemsInSphere.Values.Where(s => !systemNames.Contains(s.Name!)).Select(s => s.Name!).Distinct().ToArray();

        if (nonCachedSystemNames.Length > 0)
        {
            Info($"Fetching {nonCachedSystemNames.Length} non-cached systems with bodies from EDSM.");
            var nonCachedSystems = FetchSystems(nonCachedSystemNames).ToArray();
            Info($"Fetched {nonCachedSystems} systems from EDSM.");

            if (nonCachedSystems.Length > 0)
            {
                foreach (var system in nonCachedSystems)
                {
                    if (systemsInSphere.TryGetValue(system.Name!, out var systemWithCoordinates))
                    {
                        if (systemWithCoordinates.Coordinates != null)
                            system.Coordinates = systemWithCoordinates.Coordinates;
                    }
                }

                systems = systems.Concat(nonCachedSystems).ToArray();
                SaveSystems(systems);
                Info($"Saved {systems.Length} systems to cache.");
            }
        }
    }

    private EdsmSystemSummary[] FetchSystemsInSphere(string centralSystemName, int radius)
    {
        var parameters = new EdsmGetSystemsInSphereParameters
        {
            SystemName = centralSystemName,
            Radius = radius,
            ShowCoordinates = true
        };

        var json = _edsmClient.GetSystemsInSphere(parameters);

        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            Error("Empty response received from EDSM for systems in sphere.");
            return [];
        }

        return EdsmSystemSummary.ParseArray(json);
    }


    private IEnumerable<EdsmSystem> FetchSystems(string[] systemNames)
    {
        var total = systemNames.Length;
        foreach (var (index, systemName) in systemNames.WithIndex())
        {
            Info($"Fetching SystemID {systemName} ({index + 1} of {total}) from EDSM.");

            var parameters = new EdsmGetSystemParameters
            {
                SystemName = systemName
            };

            _edsmClient.Throttle();
            var json = _edsmClient.GetSystemBodies(parameters);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Warning($"Empty response for SystemID {systemName}.");
                continue;
            }

            var system = EdsmSystem.ParseWithBodies(json);

            yield return system;
        }
    }


    private EdsmSystem[] LoadSystems()
    {
        if (!SystemCacheFile.Exists)
            return [];

        using var stream = SystemCacheFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        var task = MemoryPackSerializer.DeserializeAsync<EdsmSystem[]>(stream);
        task.AsTask().Wait();
        return task.Result ?? [];
    }

    private void SaveSystems(EdsmSystem[] systems)
    {
        using var stream = SystemCacheFile.Open(FileMode.Create, FileAccess.Write, FileShare.Write);
        var task = MemoryPackSerializer.SerializeAsync(stream, systems);
        task.AsTask().Wait();
    }

    private FileInfo SystemCacheFile => _systemCacheFile ??= GetSystemCacheFile();

    private FileInfo? _systemCacheFile;

    private FileInfo GetSystemCacheFile() => new FileInfo(Path.Combine(DataDirectory.FullName, "systems.cache"));


    private DirectoryInfo DataDirectory => _dataDirectory ??= GetDataDirectory();

    private DirectoryInfo? _dataDirectory;

    private static DirectoryInfo GetDataDirectory()
    {
        var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppDataDir, "EDExoBioSystemFinder");
        var dirInfo = new DirectoryInfo(dataDir);
        if (!dirInfo.Exists)
            dirInfo.Create();
        return dirInfo;
    }
}