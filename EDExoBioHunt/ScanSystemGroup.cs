using Octurnion.EliteDangerousUtils;

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
    
    public ICoordinates Center => Cuboid.Center;

    public double Radius => _radius ??= ComputeRadius();

    private double? _radius;

    private double ComputeRadius()
    {
        var center = Center;
        return _systems.Values.Max(s => s.Distance(center));
    }

    public Cuboid Cuboid => _boundingBox ??= new Cuboid(_systems.Values);

    private Cuboid? _boundingBox;

    public ICoordinates MeanCentroid => _meanCentroid ??= CoordinatesExtensions.ComputeMeanCentroid(Systems);

    private ICoordinates? _meanCentroid;

    public override string ToString() => $"{Count} systems, centered on [{Center}] (nearest {CenterSystem.System.Name} @ [{CenterSystem}], {CenterSystem.Distance(Center)} LY off-center), size [{Cuboid.Size}], radius {Radius:F1}";
}