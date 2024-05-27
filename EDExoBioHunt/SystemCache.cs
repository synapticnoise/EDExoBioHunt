using MemoryPack;
using Octurnion.Common.Utils;
using Octurnion.EliteDangerousUtils;
using Octurnion.EliteDangerousUtils.EDSM;
using Octurnion.EliteDangerousUtils.EDSM.Client;

namespace EDExoBioHunt;

public class SystemCache
{
    private readonly StatusUpdateDelegate? _statusUpdateDelegate;
    private readonly EdsmClient _edsmClient;

    private EdsmSystem[] _systems;

    public SystemCache(StatusUpdateDelegate? statusUpdateDelegate = null, EdsmClient? edsmClient = null)
    {
        _statusUpdateDelegate = statusUpdateDelegate;
        _edsmClient = edsmClient ?? new EdsmClient();
        _systems = LoadSystems();
    }

    public IDictionary<string, EdsmSystem> SystemsByName => _systemsByName ??= _systems.ToDictionary(s => s.Name!, StringComparer.InvariantCultureIgnoreCase);

    private Dictionary<string, EdsmSystem>? _systemsByName;

    public IEnumerable<EdsmSystem> GetSystemsInSphere(ICoordinates coordinates, double radius) =>
        _systems.Where(s => s.Coordinates != null && s.Coordinates.Distance(coordinates) <= radius);

    public IEnumerable<EdsmSystem> GetSystemsInCuboid(ICuboid cuboid) =>
        _systems.Where(s => s.Coordinates != null && cuboid.Contains(s.Coordinates));

    public void CacheSystems(IEnumerable<string> systemNames)
    {
        var namesToAdd = systemNames.Where(n => !SystemsByName.ContainsKey(n)).ToArray();

        if (namesToAdd.Length == 0)
            return;

        var systemsToAdd = FetchSystemSummaries(namesToAdd).Select(s => new EdsmSystem(s)).ToArray();
        AddBodiesToSystems(systemsToAdd);

        _systems = _systems.Concat(systemsToAdd).ToArray();
        SaveSystems(_systems);
        _systemsByName = null;
    }

    public void CacheSystemsInSphere(string centralSystemName, int radius)
    {
        AddSystemSummariesToCache(FetchSystemsInSphere(centralSystemName, radius));
    }

    public void CacheSystemsInSphere(ICoordinates center, int radius)
    {
        AddSystemSummariesToCache(FetchSystemsInSphere(center, radius));
    }

    public void CacheSystemsInCube(string centralSystemName, int size)
    {
        AddSystemSummariesToCache(FetchSystemsInCube(centralSystemName, size));
    }

    public void CacheSystemsInCube(ICoordinates center, int size)
    {
        AddSystemSummariesToCache(FetchSystemsInCube(center, size));
    }

    private void AddSystemSummariesToCache(EdsmSystemSummary[] systems)
    {
        var systemsToAdd = systems.Where(s => !SystemsByName.ContainsKey(s.Name!)).Select(s => new EdsmSystem(s)).ToArray();

        if (systemsToAdd.Length == 0)
            return;

        AddBodiesToSystems(systemsToAdd);

        _systems = _systems.Concat(systemsToAdd).ToArray();
        SaveSystems(_systems);
        _systemsByName = null;
    }

    private void AddBodiesToSystems(EdsmSystem[] systems)
    {
        var systemsByName = systems.ToDictionary(s => s.Name!, StringComparer.InvariantCultureIgnoreCase);

        foreach (var systemWithBodies in FetchBodiesForSystems(systems.Select(s => s.Name!).ToArray()))
        {
            if (!systemsByName.TryGetValue(systemWithBodies.Name!, out var system))
            {
                Warning($"Failed to find system {systemWithBodies.Name!} while adding bodies to systems.");
                continue;
            }

            if (systemWithBodies.Bodies != null)
                system.Bodies = systemWithBodies.Bodies;
        }
    }

    private EdsmSystemSummary[] FetchSystemsInSphere(string centralSystemName, int radius) =>
        FetchSystemsInSphere(new EdsmGetSystemsInSphereParameters { SystemName = centralSystemName, Radius = radius });
    
    private EdsmSystemSummary[] FetchSystemsInSphere(ICoordinates center, int radius) =>
        FetchSystemsInSphere(new EdsmGetSystemsInSphereParameters { Coordinates = new EdsmCoordinates { X = center.X, Y = center.Y, Z = center.Z }, Radius = radius });
   
    private EdsmSystemSummary[] FetchSystemsInSphere(EdsmGetSystemsInSphereParameters parameters)
    {
        parameters.ShowCoordinates = true;

        _edsmClient.Throttle();
        Info("Fetching systems in sphere from EDSM.");
        var json = _edsmClient.GetSystemsInSphere(parameters);

        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            Error($"EDSM returned empty response for {nameof(_edsmClient.GetSystemsInSphere)}.");
            return [];
        }

        return EdsmSystemSummary.ParseArray(json);
    }

    private EdsmSystemSummary[] FetchSystemsInCube(string centralSystem, int size) =>
        FetchSystemsInCube(new EdsmGetSystemsInCubeParameters {SystemName = centralSystem, Size = size});

    private EdsmSystemSummary[] FetchSystemsInCube(ICoordinates center, int size) =>
        FetchSystemsInCube(new EdsmGetSystemsInCubeParameters { Coordinates = new EdsmCoordinates { X = center.X, Y = center.Y, Z = center.Z }, Size = size });

    private EdsmSystemSummary[] FetchSystemsInCube(EdsmGetSystemsInCubeParameters parameters)
    {
        parameters.ShowCoordinates = true;

        _edsmClient.Throttle();
        Info("Fetching systems in cube from EDSM.");
        var json = _edsmClient.GetSystemsInCube(parameters);

        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            Error($"EDSM returned empty response for {nameof(_edsmClient.GetSystemsInCube)}.");
            return [];
        }

        return EdsmSystemSummary.ParseArray(json);
    }

    private IEnumerable<EdsmSystemSummary> FetchSystemSummaries(string[] systemNames)
    {
        const int systemsPerCall = 10;
        var blocks = systemNames.Partition(systemsPerCall).ToArray();

        foreach (var (index, block) in blocks.WithIndex())
        {
            var parameters = new EdsmGetMultipleSystemsParameters
            {
                SystemNames = block.ToArray(),
                ShowCoordinates = true,
            };

            _edsmClient.Throttle();
            Info($"Fetching system summaries from EDSM ({index + 1} of {blocks.Length}).");
            var json = _edsmClient.GetSystems(parameters);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Error($"EDSM returned empty response for {nameof(_edsmClient.GetSystems)}.");
                continue;
            }

            EdsmSystemSummary[] systems;

            try
            {
                systems = EdsmSystemSummary.ParseArray(json);
            }
            catch (Exception e)
            {
                Error($"Failed to parse system summaries: {e.Message}");
                continue;
            }

            foreach (var system in systems)
                yield return system;
        }
    }

    private IEnumerable<EdsmSystem> FetchBodiesForSystems(string[] systemNames)
    {
        var total = systemNames.Length;
        foreach (var (index, name) in systemNames.WithIndex())
        {
            var parameters = new EdsmGetSystemParameters { SystemName = name };
            
            _edsmClient.Throttle();
            Info($"Fetching system bodies from EDSM for {name} ({index + 1} of {total}).");
            var json = _edsmClient.GetSystemBodies(parameters);

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Error($"EDSM returned empty response for {nameof(_edsmClient.GetSystemBodies)}");
                continue;
            }

            EdsmSystem system;
            
            try
            {
                system = EdsmSystem.ParseWithBodies(json);
            }
            catch (Exception e)
            {
                Error($"Failed to parse response for system {name}: {e.Message}");
                continue;
            }

            yield return system;
        }
    }

    private Dictionary<string, EdsmSystem> BuildIndex()
    {
        return _systems.ToDictionary(s => s.Name!, StringComparer.InvariantCultureIgnoreCase);
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

    private void Message(string message, StatusMessageSeverity severity) => _statusUpdateDelegate?.Invoke(message, severity);

    private void Info(string message) => Message(message, StatusMessageSeverity.Normal);
    
    private void Warning(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Warning);
    
    private void Error(string message) => _statusUpdateDelegate?.Invoke(message, StatusMessageSeverity.Error);
    
    private FileInfo SystemCacheFile => _systemCacheFile ??= GetSystemCacheFile();

    private FileInfo? _systemCacheFile;

    private FileInfo GetSystemCacheFile() => new FileInfo(Path.Combine(DataDirectory.FullName, "systems.cache"));

    private DirectoryInfo DataDirectory => _dataDirectory ??= GetDataDirectory();

    private DirectoryInfo? _dataDirectory;

    private static DirectoryInfo GetDataDirectory()
    {
        var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppDataDir, "EDExoBioHunt");
        var dirInfo = new DirectoryInfo(dataDir);
        if (!dirInfo.Exists)
            dirInfo.Create();
        return dirInfo;
    }
}