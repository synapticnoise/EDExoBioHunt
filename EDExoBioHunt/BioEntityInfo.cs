namespace EDExoBioHunt;

public class BioEntityInfo
{
    public BioEntityInfo(string id, string genus, string species, double value)
    {
        Id=id;
        Genus=genus;
        Species=species;
        Value=value;
    }

    public readonly string Id;
    public readonly string Genus;
    public readonly string Species;
    public readonly double Value;

    public override string ToString() => $"{Species} ({Id}): {Value}";
}