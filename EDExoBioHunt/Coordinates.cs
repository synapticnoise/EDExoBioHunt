using Octurnion.EliteDangerousUtils.Routing;

namespace EDExoBioHunt;

public abstract class Coordinates : ICoordinates
{
    public virtual double Distance(ICoordinates other) => Math.Sqrt(Math.Pow(Math.Abs(X - other.X), 2) + Math.Pow(Math.Abs(Y - other.Y), 2) + Math.Pow(Math.Abs(Z - other.Z), 2));
    public abstract double X { get; }
    public abstract double Y { get; }
    public abstract double Z { get; }

    public override string ToString() => $"{X}, {Y}, {Z}";
}