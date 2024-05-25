using MemoryPack;
using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;
using Octurnion.EliteDangerousUtils.Journal;
using Octurnion.EliteDangerousUtils.Journal.Entries;
using Octurnion.EliteDangerousUtils.Routing;

namespace EDExoBioHunt;

public class ExoBioAnalyser
{
    private readonly StatusUpdateDelegate? _statusUpdateDelegate;
    private readonly EdsmClient _edsmClient = new();

    public ExoBioAnalyser(StatusUpdateDelegate? statusUpdateDelegate)
    {
        _statusUpdateDelegate = statusUpdateDelegate;
    }

    public void OrganicFindingsReport()
    {
        var scans = GetEntityScans().ToArray();
        var systemNames = scans.Select(s => s.SystemName).Distinct();
        var systemsWithBodies = GetSystems(systemNames);
        var scannedEntities = GetEntityInfo().ToArray();

        bool newOrganic = true, newSystem = true;

        Console.WriteLine("Species\tValue\tSystem\tPrimary\tSize\tPlanet\tType\tAtmosphere");

        foreach (var entityInfo in scannedEntities.OrderByDescending(e => e.Value))
        {
            newOrganic = true;
            var scansForEntityBySystemName = scans.Where(s => s.SpeciesId == entityInfo.Id).GroupBy(s => s.SystemName);

            var entityValue = (entityInfo.Value / 1000000).ToString("F1");

            foreach (var gSystemName in scansForEntityBySystemName)
            {
                newSystem = true;
                
                if (!systemsWithBodies.TryGetValue(gSystemName.Key, out var system))
                    throw new KeyNotFoundException($"Failed to find system {gSystemName.Key}");

                var systemNode = system.BuildMap();
                var nodesById = systemNode.GetNodesById();

                var starCount = system.Bodies?.Count(b => b is EdsmStar) ?? 0;
                var planetCount = system.Bodies?.Count(b => b is EdsmPlanet) ?? 0;

                var systemSize = $"{starCount} S, {planetCount} P";

                foreach (var scan in gSystemName)
                {
                    if (!nodesById.TryGetValue(scan.BodyId, out var planetNode))
                        throw new KeyNotFoundException($"Failed to find body ID {scan.BodyId} in system {gSystemName.Key}");

                    if (planetNode.Body is not EdsmPlanet planet)
                        throw new InvalidOperationException($"Expected body ID {scan.BodyId} of system {gSystemName.Key} to be a planet.");
                    
                    var planetName = planet.Name ?? "<unknown>";
                    planetName = planetName.RemovePrefix(gSystemName.Key);
                    var atmosphereType = planet.AtmosphereType ?? "<unknown>";

                    Console.WriteLine(string.Join("\t", GetColumns()));

                    continue;

                    IEnumerable<string> GetColumns()
                    {
                        yield return newOrganic ? entityInfo.Species : string.Empty;
                        yield return newOrganic ? entityValue : string.Empty;
                        newOrganic = false;

                        yield return newSystem ? gSystemName.Key : string.Empty;
                        yield return newSystem ? system.GetPrimaryStarClass() ?? "<unknown>" : string.Empty;
                        yield return newSystem ? systemSize : string.Empty;
                        newSystem = false;

                        yield return planetName;
                        yield return planet.SubType ?? "<unknown>";
                        yield return atmosphereType;
                    }
                }

                continue;
            }

            continue;
        }
    }

    public void OrganicClumpFinder(double minValueMillions, double maxDistance)
    {
        var minValue = minValueMillions * 1000000;
        var entityInfoBySpeciesId = GetEntityInfo().ToDictionary(e => e.Id);
        var valuableScans = GetScansWithInfo().Where(t => t.info.Value >= minValue).ToArray();
        var systemNames = valuableScans.Select(t => t.scan.SystemName).Distinct().ToArray();
        var systemsWithBodies = GetSystems(systemNames);
        var scanSystems = GetScanSystems().ToArray();
        var groups = FindInitialGroups().ToList();
        ExpandGroups();
        MergeGroups();

        foreach (var (index, group) in groups.OrderByDescending(g => g.Count).ThenByDescending(g => g.Radius).WithIndex())
        {
            Console.WriteLine();
            Console.WriteLine($"Group #{index+1}: {group.Count} systems, centered on [{group.Center}] in a {group.Radius:F1} LY radius.");

            foreach (var sg in group.Systems.OrderBy(sg => sg.Distance(group.Center)))
            {
                Console.WriteLine($"  {sg.System.Name} [{sg}], Distance from center {sg.Distance(group.Center):F1} LY.");
                foreach (var (info, count) in GetScanSummary().OrderByDescending(t => t.info.Value).ThenByDescending(t => t.count))
                {
                    Console.WriteLine($"    {info.Species} ({(info.Value / 1000000):F1} MCr): {count} occurrence{(count > 1 ? "s" : string.Empty)}");
                }

                continue;

                IEnumerable<(BioEntityInfo info, int count)> GetScanSummary()
                {
                    foreach (var gSpecies in sg.Scans.GroupBy(s => s.SpeciesId))
                    {
                        if (!entityInfoBySpeciesId.TryGetValue(gSpecies.Key, out var entityInfo))
                            throw new KeyNotFoundException($"Could not find species info for {gSpecies.Key}.");

                        yield return (entityInfo, gSpecies.Count());
                    }
                }
            }
        }
        
        return;

        void MergeGroups()
        {
            bool didMerge;
            do
            {
                didMerge = false;
                foreach (var main in groups.OrderByDescending(g => g.Radius))
                {
                    if (FindMergeCandidate(main, out var toMerge))
                    {
                        foreach(var ssg in toMerge!.Systems)
                            main.AddSystem(ssg);
                        groups.Remove(toMerge);
                        didMerge = true;
                        break;
                    }
                }

            } while (didMerge);

            return;

            bool FindMergeCandidate(ScanSystemGroup group, out ScanSystemGroup? toConsume)
            {
                toConsume = null;
                var candidates = groups
                    .Where(g => !ReferenceEquals(g, group))
                    .Select(g => (g, g.Center.Distance(group.Center)))
                    .OrderBy(g => g.Item2);

                foreach (var (candidate, distance) in candidates)
                {
                    if (distance <= maxDistance)
                    {
                        toConsume = candidate;
                        return true;
                    }
                }

                return false;
            }
        }

        void ExpandGroups()
        {
            var alreadyGrouped = new HashSet<string>(groups.SelectMany(g => g.Systems).Select(s => s.System.Name!));
            var candidates = scanSystems.Where(s => !alreadyGrouped.Contains(s.System.Name!)).ToDictionary(s => s.System.Name!);

            foreach (var group in groups)
            {
                List<string> toRemove = [];
                foreach (var (name, system) in candidates)
                {
                    if (system.Distance(group.BoundingBox.Center) <= maxDistance)
                    {
                        group.AddSystem(system);
                        toRemove.Add(name);
                    }
                }

                foreach (var name in toRemove)
                    candidates.Remove(name);
            }
        }

        IEnumerable<ScanSystemGroup> FindInitialGroups()
        {
            var candidates = scanSystems.ToDictionary(s => s.System.Name!);

            while (GetNextGrouping(out var sa, out var sb))
            {
                candidates.Remove(sa!.System.Name!);
                candidates.Remove(sb!.System.Name!);
                yield return new ScanSystemGroup([sa, sb]);
            }

            yield break;

            bool GetNextGrouping(out ScanSystem? a, out ScanSystem? b)
            {
                var groupings = GetCandidateGroupings().ToArray();
                if (groupings.Length == 0)
                {
                    a = null;
                    b = null;
                    return false;
                }

                var best = groupings.MinBy(g => g.a.Distance(g.b));

                a = best.a;
                b = best.b;
                return true;
            }

            IEnumerable<(ScanSystem a, ScanSystem b)> GetCandidateGroupings()
            {
                foreach (var a in candidates.Values)
                {
                    foreach(var b in candidates.Where(p => p.Key != a.System.Name).Select(p => p.Value))
                    {
                        if (a.Distance(b) <= maxDistance)
                            yield return (a, b);
                    }
                }
            }
        }

        IEnumerable<ScanSystem> GetScanSystems()
        {
            foreach (var gSystem in valuableScans.GroupBy(t => t.scan.SystemName))
            {
                if (!systemsWithBodies.TryGetValue(gSystem.Key, out var system))
                {
                    Warning($"Could not find scan system {gSystem.Key}.");
                    continue;
                }

                yield return new ScanSystem(system, gSystem.Select(t => t.scan).ToArray());
            }
        }

        IEnumerable<(BioEntityScan scan, BioEntityInfo info)> GetScansWithInfo()
        {
            foreach (var scan in GetEntityScans())
            {
                if(entityInfoBySpeciesId.TryGetValue(scan.SpeciesId, out var info))
                    yield return (scan, info);
                else 
                    Warning($"No info found for species ID {scan.SpeciesId}.");
            }
        }
    }

    private IEnumerable<BioEntityInfo> GetEntityInfo()
    {
        var entriesBySpeciesId =
            RelevantEntries
                .OfType<SellOrganicDataJournalEntry>()
                .SelectMany(e => e.BioData ?? [])
                .Where(e => !string.IsNullOrEmpty(e.Species))
                .GroupBy(e => e.Species);

        foreach (var gSpecies in entriesBySpeciesId)
        {
            var genusNames = gSpecies.Select(e => e.GenusLocalised).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            var speciesNames = gSpecies.Select(e => e.SpeciesLocalised).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            var values = gSpecies.Select(e => e.Value).Where(v => v.HasValue).Distinct().ToArray();

            if (genusNames.Length == 0)
            {
                Warning($"No genus names found for species {gSpecies.Key}");
                continue;
            }

            if (speciesNames.Length == 0)
            {
                Warning($"No species names found for species {gSpecies.Key}");
                continue;
            }

            if (values.Length == 0)
            {
                Warning($"No values found for species {gSpecies.Key}");
                continue;
            }

            if (genusNames.Length > 1)
                Warning($"Multiple genus names found for species {gSpecies.Key}");

            var genusName = genusNames[0]!;

            if (speciesNames.Length > 1)
                Warning($"Multiple species names found for species {gSpecies.Key}");
            
            var speciesName = speciesNames[0]!;

            if (values.Length > 1)
                Warning($"Multiple values found for species {gSpecies.Key}");

            var value = values[0]!.Value;

            yield return new BioEntityInfo(gSpecies.Key, genusName, speciesName, value);
        }
    }

    private IEnumerable<BioEntityScan> GetEntityScans()
    {
        string? systemName = null;

        foreach (var entry in RelevantEntries)
        {
            switch (entry)
            {
                case TouchdownJournalEntry touchdown:
                    if(!string.IsNullOrWhiteSpace(touchdown.StarSystem))
                        systemName = touchdown.StarSystem;
                    break;

                case ScanOrganicJournalEntry scan:
                    if (scan.ScanType == "Analyse" && systemName != null)
                    {
                        if (string.IsNullOrWhiteSpace(scan.Species) || !scan.SystemAddress.HasValue || !scan.Body.HasValue)
                        {
                            Warning($"Discarding invalid scan entry \"{systemName}\", \"{scan.SystemAddress}\", \"{scan.Body}\", \"{scan.Species}\".");
                            continue;
                        }

                        yield return new BioEntityScan(systemName!, scan.SystemAddress.Value, scan.Body.Value, scan.Species);
                    }

                    break;
            }
        }
    }
    
    private JournalEntry[] RelevantEntries => _relevantEntries ??= GetRelevantEntries().ToArray();

    private JournalEntry[]? _relevantEntries;

    private void Info(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Normal);

    private void Warning(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Warning);

    private void Error(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Error);

    private static IEnumerable<JournalEntry> GetRelevantEntries()
    {
        var baseType = typeof(JournalEntry);
        return JournalFile.GetJournalEntries().Where(e => e.Entry != null && e.Entry.GetType() != baseType && e.Entry.Timestamp.HasValue).Select(e => e.Entry).OrderBy(e => e!.Timestamp)!;
    }

    private IDictionary<string, EdsmSystem> GetSystems(IEnumerable<string> systemNames)
    {
        var existingSystems = LoadSystems();
        Info($"Loaded {existingSystems.Length} systems.");
        var existingSystemNames = existingSystems.Select(s => s.Name!).ToArray();
        var namesToQuery = systemNames.Except(existingSystemNames, StringComparer.InvariantCultureIgnoreCase).Distinct().ToArray();

        var needToSave = false;

        if (namesToQuery.Length > 0)
        {
            Info($"Fetching {namesToQuery.Length} new systems.");
            var fetchedSystems = FetchSystems(namesToQuery).ToArray();

            if (fetchedSystems.Length > 0)
            {
                Info($"Adding {fetchedSystems.Length} new systems to cache.");
                existingSystems = existingSystems.Concat(fetchedSystems).ToArray();
                needToSave = true;
            }
        }

        if (existingSystems.Any(s => s.Coordinates == null))
        {
            needToSave = FetchSystemCoordinates(existingSystems) || needToSave;
        }

        if (needToSave)
        {
            SaveSystems(existingSystems);
            Info($"Saved {existingSystems.Length} systems.");
        }

        return existingSystems.ToDictionary(s => s.Name!, StringComparer.InvariantCultureIgnoreCase);
    }

    private IEnumerable<EdsmSystem> FetchSystems(string[] systemNames)
    {
        var total = systemNames.Length;
        foreach (var (index, systemName) in systemNames.WithIndex())
        {
            Info($"Fetching SystemID {systemName} ({index+1} of {total}) from EDSM.");
            
            var parameters = new EdsmGetSystemParameters
            {
                SystemName = systemName,
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

    private bool FetchSystemCoordinates(EdsmSystem[] systems)
    {
        const int systemsPerCall = 10;

        var systemsNeedingCoordinates = systems.Where(s => s.Coordinates == null).ToArray();
        if (systemsNeedingCoordinates.Length == 0)
            return false;

        var systemsByName = systems.ToDictionary(s => s.Name!);

        Info($"Fetching coordinates from EDSM for {systemsNeedingCoordinates.Length} systems.");
        var systemNames = systemsNeedingCoordinates.Select(s => s.Name!).ToArray();

        var updateCount = 0;
        var blocks = systemNames.Partition(systemsPerCall).ToArray();
        foreach (var (index, block) in blocks.WithIndex())
        {
            var parameters = new EdsmGetMultipleSystemsParameters
            {
                SystemNames = block.ToArray(),
                ShowCoordinates = true
            };

            Info($"Fetching block {index+1} of {blocks.Length}");
            _edsmClient.Throttle();
            var json = _edsmClient.GetSystems(parameters);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Warning($"Empty response received from EDSM.");
                continue;
            }

            var fetchedSystems = EdsmSystemSummary.ParseArray(json);

            foreach (var system in fetchedSystems)
            {
                if (system.Coordinates == null)
                    continue;

                if (!systemsByName.TryGetValue(system.Name!, out var toUpdate))
                {
                    Warning($"Could not find returned system {system.Name} in dictionary.");
                    continue;
                }

                toUpdate.Coordinates = system.Coordinates;
                updateCount++;
            }
        }

        Info($"Updated coordinates for {updateCount} systems.");
        return updateCount > 0;
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
        var dataDir = Path.Combine(localAppDataDir, "EDExoBioAnalyser");
        var dirInfo = new DirectoryInfo(dataDir);
        if (!dirInfo.Exists)
            dirInfo.Create();
        return dirInfo;
    }
}