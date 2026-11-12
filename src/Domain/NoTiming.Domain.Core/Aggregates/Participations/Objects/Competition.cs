namespace NoTiming.Domain.Core.Aggregates.Participations.Objects;

public record Competition
{
    public Competition(string name, bool englishNames)
    {
        Name = name;
        EnglishNames = englishNames;
    }

    public string Name { get; }
    public bool EnglishNames { get; }

    public override string ToString()
    {
        return EnglishNames ? $"{Name} (English names)" : Name;
    }
}
