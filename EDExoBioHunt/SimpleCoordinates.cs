using Octurnion.EliteDangerousUtils.EDSM;

namespace EDExoBioHunt;

public class SimpleCoordinates : Coordinates
{
    public SimpleCoordinates(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public SimpleCoordinates(EdsmCoordinates coordinates)
    {
        X = coordinates.X;
        Y = coordinates.Y;
        Z = coordinates.Z;
    }

    public override double X { get; }
    public override double Y { get; }
    public override double Z { get; }
}