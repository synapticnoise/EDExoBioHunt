using MemoryPack;
using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;

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
        var systems = LoadSystems();
        var filteredSystemsByPrefix = CategorizeSystems(systems)
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
                                if(!atDict.TryAdd(planet.AtmosphereType, 1))
                                    atDict[planet.AtmosphereType]++;
                            }

                            if (!string.IsNullOrWhiteSpace(planet.SubType))
                            {
                                if(!ptDict.TryAdd(planet.SubType, 1))
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

    private static string GetBreakoutPrefix(SystemNameBreakout breakout) =>
        $"{breakout.Region} {breakout.Boxel} {breakout.MassCode}{breakout.MajorSeries}";
    
    private static IEnumerable<(EdsmSystem system, SystemNameBreakout breakout)> CategorizeSystems(EdsmSystem[] systems)
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
        
        var systemsInSphere = FetchSystemsInSphere(centralSystemName, radius);
        Info($"Fetched {systemsInSphere.Length} system summaries from EDSM.");
            
        var systemNames = new HashSet<string>(systems.Select(s => s.Name!), StringComparer.InvariantCultureIgnoreCase);
        var nonCachedSystemNames = systemsInSphere.Where(s => !systemNames.Contains(s.Name!)).Select(s => s.Name!).Distinct().ToArray();

        if (nonCachedSystemNames.Length > 0)
        {
            Info($"Fetching {nonCachedSystemNames.Length} non-cached systems with bodies from EDSM.");
            var nonCachedSystems = FetchSystems(nonCachedSystemNames).ToArray();
            Info($"Fetched {nonCachedSystems} systems from EDSM.");

            if (nonCachedSystems.Length > 0)
            {
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