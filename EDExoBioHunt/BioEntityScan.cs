namespace EDExoBioHunt;

public class BioEntityScan
{
    public BioEntityScan(string systemName, long systemId, int bodyId, string speciesId)
    {
        SystemName = systemName;
        SystemId = systemId;
        BodyId = bodyId;
        SpeciesId = speciesId;
    }

    public readonly string SystemName;
    public readonly long SystemId;
    public readonly int BodyId;
    public readonly string SpeciesId;

    public override string ToString() => $"{SystemName} ({SystemId}) @ Body ID({BodyId}): {SpeciesId}";
}