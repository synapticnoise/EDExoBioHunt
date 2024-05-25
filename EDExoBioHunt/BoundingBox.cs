using Octurnion.EliteDangerousUtils.Routing;

namespace EDExoBioHunt;

public class BoundingBox
{
    public BoundingBox(IEnumerable<ICoordinates> coordinates)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var c in coordinates)
        {
            if (c.X < minX) minX = c.X;
            if (c.Y < minY) minY = c.Y;
            if (c.Z < minZ) minZ = c.Z;
            if (c.X > maxX) maxX = c.X;
            if (c.Y > maxY) maxY = c.Y;
            if (c.Z > maxZ) maxZ = c.Z;
        }

        Min = new SimpleCoordinates(minX, minY, minZ);
        Max = new SimpleCoordinates(maxX, maxY, maxZ);
        Size = new SimpleCoordinates(maxX - minX, maxY - minY, maxZ - minZ);
        Center = new SimpleCoordinates(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2, minZ + (maxZ - minZ) / 2);
    }

    public readonly ICoordinates Min;
    public readonly ICoordinates Max;
    public readonly ICoordinates Size;
    public readonly ICoordinates Center;

    public override string ToString() => $"Center [{Center}], Size [{Size}], Min [{Min}], Max[{Max}]";
}