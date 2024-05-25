using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;
using Octurnion.EliteDangerousUtils.Journal;
using Octurnion.EliteDangerousUtils.Journal.Entries;

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
        var systemCache = new SystemCache(Message, EdsmClient);
        systemCache.CacheSystems(["Pru Euq AS-S c17-0", "Pru Euq AS-S c17-1", "Pru Euq AS-S c17-2", "Pru Euq AS-S c17-3", "Pru Euq AS-S c17-4", "Pru Euq AS-S c17-5"]);
        systemCache.CacheSystemsInSphere("Pru Euq CN-S c17-32", 30);

        //var analyser = new ExoBioAnalyser(Message);
        //analyser.OrganicClumpFinder(5, 20);


        //var finder = new ExoBioSystemFinder(Message);
        //finder.FindMassCodeSystemsInSphere("Pru Euq CN-S c17-32", 50, ["c", "d"]);
        //var filename = args[0];
        //var file = new FileInfo(filename);
        //finder.BuildHuntList(file);

        //MassCodeHunt("Pru Euq LD-K d8-75", 50, "d");

        //var prefix = args[0];
        //var min = int.Parse(args[1]);
        //int max = int.Parse(args[2]);

        //MinorSeriesHunt(prefix, min, max);


        //if (args.Length > 0)
        //{
        //    var systemName = args[0];
        //    FetchSystems(systemName, Radius);
        //    FetchBodies(Console.WriteLine);
        //}

        //var systems = LoadSystems();

        //CategorizeSystems(systems);

        //FindInterestingSystems(systems, AtmosphereExclusions());

        return;

        //IEnumerable<string> AtmosphereExclusions()
        //{
        //    yield return "no atmosphere";
        //    yield return "thin argon";
        //}

        //var systemsWithBodies = systems.Where(s => s.Bodies?.Length > 0).ToArray();

        //var allAtmosphereTypes = systemsWithBodies
        //    .SelectMany(s => s.Bodies!)
        //    .OfType<EdsmPlanet>()
        //    .Where(p => p.IsLandable && !string.IsNullOrEmpty(p.AtmosphereType))
        //    .Select(p => p.AtmosphereType)
        //    .Distinct().ToArray();

        //var visitedSystemNames = new HashSet<string>(GetVisitedSystemNames());
        //var nonVisitedSystems = systemsWithBodies.Where(s => !visitedSystemNames.Contains(s.Name!)).ToArray();
        //var relevantSystems = nonVisitedSystems.Where(IsRelevantSystem).ToArray();

        //Console.WriteLine("System\tAtmosphereTypes");

        //foreach (var system in relevantSystems)
        //{
        //    var atmosphereTypes = system.Bodies!
        //        .OfType<EdsmPlanet>()
        //        .Where(p => p.IsLandable && !string.IsNullOrEmpty(p.AtmosphereType))
        //        .Select(p => p.AtmosphereType)
        //        .Distinct().ToArray();

        //    Console.WriteLine($"{system.Name}\t{string.Join(",", atmosphereTypes)}");
        //}


        //bool IsRelevantSystem(EdsmSystem system)
        //{
        //    if(visitedSystemNames.Contains(system.Name!))
        //        return false;

        //    return system.Bodies!.Any(IsRelevantBody);
        //}

        //static bool IsRelevantBody(EdsmBody body)
        //{
        //    if (!(body is EdsmPlanet planet))
        //        return false;

        //    if (!planet.IsLandable)
        //        return false;

        //    switch (planet.AtmosphereType!.ToLowerInvariant())
        //    {
        //        case "no atmosphere":
        //        case "thin argon":
        //        case "thin argon-rich":
        //            return false;
        //    }

        //    return true;
        //}
    }

    //private static void FindAllAtmosphereTypes(EdsmSystem[] systems)
    //{
    //    var atmosphereTypes = systems.SelectMany(s => s.Bodies?.Select(GetAtmosphereType) ?? []).Distinct().ToArray();

    //    foreach (var atmosphereType in atmosphereTypes)
    //        Console.WriteLine(atmosphereType);
    //}

    //private static void FindInterestingSystems(EdsmSystem[] systems, IEnumerable<string> atmosphereTypesToExclude)
    //{
    //    var excludeSet = new HashSet<string>(atmosphereTypesToExclude, StringComparer.InvariantCultureIgnoreCase);
    //    var fsdJumps = JournalFile.GetEntries(GetJournalDirectory(), JournalEntryFactory).OfType<FsdJumpJournalEntry>();
    //    var visitedSystemsNames = new HashSet<string>(fsdJumps.Select(e => e.StarSystem));
    //    var interestingSystems = systems.Where(s => !visitedSystemsNames.Contains(s.Name!) && s.Bodies?.Any(IsInterestingPlanet) == true).ToArray();

    //    Console.WriteLine("System\tX\tY\tZ\tAtmosphere Types");

    //    foreach (var system in interestingSystems)
    //    {
    //        var atmosphereTypes = system.Bodies?.Select(GetAtmosphereType).Where(AtmosphereFilter).Distinct() ?? [];
    //        Console.WriteLine($"{system.Name}\t{system.Coordinates.X}\t{system.Coordinates.Y}\t{system.Coordinates.Z}\t{string.Join(", ", atmosphereTypes)}");

    //        static bool AtmosphereFilter(string? type) => !string.IsNullOrEmpty(type) && !type.Equals("no atmosphere", StringComparison.InvariantCultureIgnoreCase);

    //    }

    //    return;

    //    bool IsInterestingPlanet(EdsmBody body)
    //    {
    //        var atmosphereType = GetAtmosphereType(body);

    //        if (atmosphereType == null) return false;

    //        if (excludeSet.Contains(atmosphereType)) return false;

    //        return true;
    //    }
    //}

    //private static void CategorizeSystems(EdsmSystem[] systems)
    //{
    //    var visitedSystemNames = new HashSet<string>(GetVisitedSystemNames(), StringComparer.InvariantCultureIgnoreCase);
    //    var categorizedSystems = CategorizeSystemsByName(systems).ToArray();

    //    Console.WriteLine("Region\tBoxel\tMC\tMajor\tMinor\tAtmosphere Types");

    //    var byRegion = categorizedSystems.GroupBy(s => s.Region);
    //    foreach (var gRegion in byRegion)
    //    {
    //        var byBoxel = gRegion.GroupBy(s => s.Boxel).OrderBy(g => g.Key);
    //        foreach (var gBoxel in byBoxel.OrderBy(g => g.Key))
    //        {
    //            var byMassCode = gBoxel.GroupBy(s => s.MassCode).OrderBy(g => g.Key);
    //            foreach (var gMassCode in byMassCode)
    //            {
    //                var byMajorSeries = gMassCode.GroupBy(s => s.MajorSeries).OrderBy(g => g.Key);
    //                foreach (var gMajorSeries in byMajorSeries)
    //                {
    //                    var planets = gMajorSeries.SelectMany(s => s.System.Bodies ?? []).OfType<EdsmPlanet>().Where(p => p.IsLandable);
    //                    var atDict = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
    //                    foreach (var planet in planets)
    //                    {
    //                        if (string.IsNullOrEmpty(planet.AtmosphereType) || planet.AtmosphereType.Equals("no atmosphere", StringComparison.InvariantCultureIgnoreCase))
    //                            continue;

    //                        if (!atDict.TryAdd(planet.AtmosphereType!, 1))
    //                            atDict[planet.AtmosphereType!]++;
    //                    }

    //                    var atmosphereTypes = string.Join(", ", atDict.Select(p => $"{p.Key} ({p.Value})"));

    //                    var gMinorSeries = gMajorSeries
    //                        .Where(s => !visitedSystemNames.Contains(s.System.Name!))
    //                        .OrderBy(s => s.MinorSeries)
    //                        .Select(s => s.MinorSeries.ToString());

    //                    Console.WriteLine($"{gRegion.Key}\t{gBoxel.Key}\t{gMassCode.Key}\t{gMajorSeries.Key}\t{string.Join(", ", gMinorSeries)}\t{atmosphereTypes}");
    //                }
    //            }
    //        }
    //    }
    //}

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


    private static void MassCodeHunt(string centralSystemName, int radius, string massCode)
    {
        var systems = GetSystemsInRadius(centralSystemName, radius).ToDictionary(s => s.Name!);
        var breakouts = systems.Values.Select(s => SystemNameBreakout.Parse(s.Name!)).Where(b => b != null && b.MassCode.Equals(massCode, StringComparison.InvariantCultureIgnoreCase));
        var breakoutsByPrefix = breakouts.GroupBy(GetPrefix);

        Console.WriteLine("System\tClass\tBodies\tX\tY\tZ");

        foreach (var gPrefix in breakoutsByPrefix)
        {
            Console.WriteLine();

            foreach (var b in gPrefix)
            {
                var s = systems[GetName(b)];
                Console.WriteLine($"{GetName(b)}\t{s.PrimaryStar?.Type}\t{s.BodyCount}\t{s.Coordinates?.X}\t{s.Coordinates?.Y}\t{s.Coordinates?.Z}");
            }
        }

        return;

        static string GetPrefix(SystemNameBreakout? b) => $"{b!.Region} {b.Boxel} {b.MassCode}{b.MajorSeries}-";
        static string GetName(SystemNameBreakout? b) => $"{GetPrefix(b)}{b.MinorSeries}";
    }


    //private static void MinorSeriesHunt(string systemNamePrefix, int minMinorIndex, int maxMinorIndex)
    //{
    //    Console.WriteLine("System\tBodies\tStar\tX\tY\tZ");

    //    foreach (var system in GetSystemsByPrefixAndMinorIndexRange(systemNamePrefix, minMinorIndex, maxMinorIndex))
    //    {
    //        Console.WriteLine($"{system.Name}\t{system.BodyCount}\t{system.PrimaryStar?.Type}\t{system.Coordinates?.X}\t{system.Coordinates?.Y}\t{system.Coordinates?.Z}");
    //    }
    //}

    //private static IEnumerable<EdsmSystemSummary> GetSystemsByPrefixAndMinorIndexRange(string systemNamePrefix, int minMinorIndex, int maxMinorIndex)
    //{
    //    const int systemsPerCall = 10;

    //    var visitedSystemNames = new HashSet<string>(GetVisitedSystemNames(), StringComparer.CurrentCulture);
    //    var systemNames = Enumerable.Range(minMinorIndex, maxMinorIndex - minMinorIndex + 1).Select(i => $"{systemNamePrefix}{i}").Where(n => !visitedSystemNames.Contains(n));
    //    var blocks = systemNames.Partition(systemsPerCall);

    //    foreach (var block in blocks)
    //    {
    //        var parameters = new EdsmGetMultipleSystemsParameters
    //        {
    //            SystemNames = block.ToArray(),
    //            ShowCoordinates = true,
    //            ShowPrimaryStar = true,
    //            ShowInformation = true,
    //            ShowId = true
    //        };

    //        EdsmClientThrottle();
    //        var json = EdsmClient.GetSystems(parameters);

    //        var systems = EdsmSystemSummary.ParseArray(json).ToDictionary(s => s.Name, StringComparer.InvariantCultureIgnoreCase);

    //        foreach (var name in parameters.SystemNames)
    //        {
    //            if (systems.TryGetValue(name, out var foundSystem))
    //            {
    //                yield return foundSystem;
    //                continue;
    //            }

    //            yield return new EdsmSystemSummary
    //            {
    //                Name = name
    //            };
    //        }
    //    }
    //}

    //private static string? GetAtmosphereType(EdsmBody body)
    //{
    //    if (body is not EdsmPlanet planet) return null;

    //    if (!planet.IsLandable) return null;

    //    if (string.IsNullOrEmpty(planet.AtmosphereType)) return null;

    //    return planet.AtmosphereType;
    //}

    //private static void FetchSystems(string systemName)
    //{
    //    EdsmSystem[] existingSystems = [];
    //    HashSet<string> existingSystemNames = [];

    //    if (File.Exists(SystemsFilename))
    //    {
    //        Console.WriteLine("Reading existing systems.");
    //        existingSystems = LoadSystems();
    //        foreach (var system in existingSystems)
    //            existingSystemNames.Add(system.Name!);
    //    }

    //    var systemsInSphere = GetSystemsInRadius(systemName);
    //    Console.WriteLine("Fetching systems in sphere.");
    //    var systemNamesToFetch = systemsInSphere.Where(IsInteresting).Select(s => s.Name).ToArray();

    //    if (systemNamesToFetch.Length == 0)
    //        return;

    //    var systemsWithBodies = GetSystemsWithBodies(systemNamesToFetch!, UpdateStatus).ToArray();

    //    var combinedSystems = existingSystems.Concat(systemsWithBodies).GroupBy(s => s.Name).Select(g => g.First()).ToArray();

    //    Console.WriteLine($"Writing {SystemsFilename}");
    //    using var stream = new FileStream(SystemsFilename, FileMode.Create, FileAccess.Write, FileShare.Read);
    //    var task = MemoryPackSerializer.SerializeAsync(stream, combinedSystems);
    //    task.AsTask().Wait();

    //    return;

    //    static void UpdateStatus(string systemName, int num, int total) => Console.WriteLine($"Fetching {systemName} ({num} of {total})");

    //    bool IsInteresting(EdsmSystemSummary system) => !string.IsNullOrEmpty(system.Name) && !existingSystemNames.Contains(system.Name) && system.BodyCount > 1;
    //}

    //private static void FetchSystems(string centerSystemName, int radius)
    //{
    //    Console.WriteLine("Loading existing systems.");

    //    var existingSystems = LoadSystems().ToDictionary(s => s.Name!);

    //    Console.WriteLine($"Loaded {existingSystems.Count} systems.");
    //    Console.WriteLine($"Fetching systems within {radius} LY of {centerSystemName}.");

    //    var systemsInSphere = GetSystemsInRadius(centerSystemName, radius);

    //    Console.WriteLine($"Fetched {systemsInSphere.Length} systems.");

    //    var newSystems = new List<EdsmSystem>();

    //    foreach (var system in systemsInSphere)
    //    {
    //        if (existingSystems.TryGetValue(system.Name!, out var existingSystem))
    //        {
    //            if(existingSystem.Coordinates == null && system.Coordinates != null)
    //                existingSystem.Coordinates = system.Coordinates;
    //            continue;
    //        }

    //        newSystems.Add(new EdsmSystem()
    //        {
    //            Name = system.Name,
    //            Id32 = system.Id32,
    //            Id64 = system.Id64,
    //            Coordinates = system.Coordinates,
    //            Bodies = system.BodyCount > 0 ? [] : null
    //        });
    //    }

    //    Console.WriteLine($"New systems added: {newSystems.Count}");

    //    Console.WriteLine($"Saving systems.");

    //    var combinedSystems = existingSystems.Values.Concat(newSystems).ToArray();
    //    SaveSystems(combinedSystems);
    //}

    //private static void FetchBodies(Action<string> statusUpdater)
    //{
    //    var systems = LoadSystems();
    //    var systemsToFetch = systems.Where(s => s.Bodies is { Length: 0 }).ToArray();

    //    if (systemsToFetch.Length == 0)
    //        return;

    //    var total = systemsToFetch.Length;
    //    foreach (var (index, system) in systemsToFetch.WithIndex())
    //    {
    //        var bodies = FetchBodiesForSystem(system.Name!, index + 1, total, statusUpdater);
    //        system.Bodies = bodies.Length > 0 ? bodies : null;
    //    }

    //    SaveSystems(systems);
    //}

    //private static EdsmBody[] FetchBodiesForSystem(string systemName, int current, int total, Action<string> statusUpdater)
    //{
    //    var parameters = new EdsmGetSystemParameters
    //    {
    //        SystemName = systemName
    //    };

    //    var retryCount = -1;
    //    string? json = null;
    //    while (retryCount++ < 100)
    //    {
    //        json = null;

    //        try
    //        {
    //            EdsmClientThrottle();
    //            statusUpdater($"Fetching bodies for {systemName} ({current} of {total}){(retryCount > 0 ? $" (Retry {retryCount})" : string.Empty)}");
    //            json = EdsmClient.GetSystemBodies(parameters);
    //            if (string.IsNullOrEmpty(json) || json == "{}")
    //            {
    //                json = null;
    //                Thread.Sleep(1000);
    //                continue;
    //            }

    //            break;
    //        }
    //        catch
    //        {
    //            json = null;
    //            Thread.Sleep(1000);
    //            continue;
    //        }
    //    }

    //    if (json == null)
    //        throw new ApplicationException($"Failed to fetch bodies for system {systemName}");

    //    var system = EdsmSystem.ParseWithBodies(json);
    //    return system.Bodies ?? [];
    //}

    //private static EdsmSystem[] LoadSystems()
    //{
    //    var file = GetSystemsFile();
    //    if (!file.Exists)
    //        return [];

    //    using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
    //    var task = MemoryPackSerializer.DeserializeAsync<EdsmSystem[]>(stream);
    //    task.AsTask().Wait();
    //    if (task.Result == null)
    //        throw new ApplicationException("Null deserialization.");
    //    return task.Result;
    //}

    //private static void SaveSystems(EdsmSystem[] systems)
    //{
    //    var file = GetSystemsFile();
    //    using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
    //    var task = MemoryPackSerializer.SerializeAsync(stream, systems);
    //    task.AsTask().Wait();
    //}

    private static EdsmSystemSummary[] GetSystemsInRadius(string systemName, int radius)
    {
        var parameters = new EdsmGetSystemsInSphereParameters()
        {
            SystemName = systemName,
            Radius = radius,
            ShowPrimaryStar = true,
            ShowCoordinates = true,
            ShowId = true,
        };

        EdsmClient.Throttle();
        var json = EdsmClient.GetSystemsInSphere(parameters);

        if (json == null)
            throw new ApplicationException("Null deserialization.");

        return EdsmSystemSummary.ParseArray(json);
    }

    //private static IEnumerable<EdsmSystem> GetSystemsWithBodies(string[] systemNames, Action<string, int, int>? statusCallback = null)
    //{
    //    var total = systemNames.Length;
    //    foreach (var (index, systemName) in systemNames.WithIndex())
    //    {
    //        var parameters = new EdsmGetSystemParameters
    //        {
    //            SystemName = systemName
    //        };

    //        var retryCount = 0;
    //        string? json;
    //        while (true)
    //        {

    //            try
    //            {
    //                EdsmClientThrottle();
    //                statusCallback?.Invoke($"{systemName}{(retryCount > 0 ? $" (Retry {retryCount})" : string.Empty)}", index + 1, total);
    //                json = EdsmClient.GetSystemBodies(parameters);

    //                if (string.IsNullOrEmpty(json) || json == "{}")
    //                {
    //                    retryCount++;
    //                    Thread.Sleep(1000);
    //                    continue;
    //                }

    //                break;
    //            }
    //            catch
    //            {
    //                retryCount++;
    //                Thread.Sleep(1000);
    //            }
    //        }

    //        yield return EdsmSystem.ParseWithBodies(json);

    //    }
    //}

    private static IEnumerable<string> GetVisitedSystemNames()
    {
        return JournalFile.GetJournalEntries()
            .Select(e => e.Entry is FsdJumpJournalEntry j ? j.StarSystem : null)
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct()!;
    }

    private static DirectoryInfo GetJournalDirectory()
    {
        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new DirectoryInfo(Path.Combine(userDirectory, "Saved Games", "Frontier Developments", "Elite Dangerous"));
    }

    //private static FileInfo GetSystemsFile()
    //{
    //    const string filename = "systems.dat";
    //    return new FileInfo(Path.Combine(GetDataDirectory().FullName, filename));
    //}

    //private static DirectoryInfo GetDataDirectory()
    //{
    //    var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    //    var dataPath = Path.Combine(localAppDataPath, "EDExoBioHunt");
    //    var dataDir = new DirectoryInfo(dataPath);
    //    if (!dataDir.Exists)
    //        dataDir.Create();
    //    return dataDir;
    //}

    //private static IEnumerable<SystemNameBreakout> CategorizeSystemsByName(EdsmSystem[] systems)
    //{
    //    var expr = new Regex(@"^(?<R>.+)\s(?<B>\w\w-\w)\s(?<M>\w)(?<S1>\d+)-(?<S2>\d+)$");

    //    foreach (var system in systems)
    //    {
    //        var m = expr.Match(system.Name!);
    //        if (m.Success)
    //        {
    //            var majorSeries = int.Parse(m.Groups["S1"].Value);
    //            var minorSeries = int.Parse(m.Groups["S2"].Value);
    //            yield return new SystemNameBreakout(system, m.Groups["R"].Value, m.Groups["B"].Value, m.Groups["M"].Value, majorSeries, minorSeries);
    //        }
    //    }
    //}
}