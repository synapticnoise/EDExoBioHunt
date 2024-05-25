using Octurnion.EliteDangerousUtils.Routing;

namespace EDExoBioHunt;

public class ScanSystemGroup
{
    private readonly Dictionary<string, ScanSystem> _systems;

    public ScanSystemGroup()
    {
        _systems = [];
    }

    public ScanSystemGroup(IEnumerable<ScanSystem> systems)
    {
        _systems = systems.ToDictionary(s => s.System.Name!);
    }

    public int Count => _systems.Count;
    
    public IEnumerable<ScanSystem> Systems => _systems.Values;

    public void AddSystem(ScanSystem system)
    {
        if (_systems.TryAdd(system.System.Name!, system))
        {
            _boundingBox = null;
            _meanCentroid = null;
            _radius = null;
        }
    }

    public ScanSystem CenterSystem => _centerSystem ??= GetCenterSystem();

    private ScanSystem? _centerSystem;

    private ScanSystem GetCenterSystem()
    {
        return _systems.Values.OrderBy(s => s.Distance(Center)).First();
    }
    
    public ICoordinates Center => BoundingBox.Center;

    public double Radius => _radius ??= ComputeRadius();

    private double? _radius;

    private double ComputeRadius()
    {
        var center = Center;
        return _systems.Values.Max(s => s.Distance(center));
    }

    public BoundingBox BoundingBox => _boundingBox ??= new BoundingBox(_systems.Values);

    private BoundingBox? _boundingBox;

    public ICoordinates MeanCentroid => _meanCentroid ??= ComputeMeanCentroid();

    private SimpleCoordinates? _meanCentroid;

    private SimpleCoordinates ComputeMeanCentroid()
    {
        if (_systems.Count < 1)
            throw new InvalidOperationException($"Cannot compute centroid of an empty group.");

        double xSum = 0, ySum = 0, zSum = 0;

        foreach (var system in _systems.Values)
        {
            xSum += system.X;
            ySum += system.Y;
            ySum += system.Z;
        }

        return new SimpleCoordinates(xSum / _systems.Count, ySum / _systems.Count, zSum / _systems.Count);
    }

    public override string ToString() => $"{Count} systems, centered on [{Center}] (nearest {CenterSystem.System.Name} @ [{CenterSystem}], {CenterSystem.Distance(Center)} LY off-center), size [{BoundingBox.Size}], radius {Radius:F1}";
}