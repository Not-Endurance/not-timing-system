namespace NTS.Domain.Core.Aggregates.Participations.Objects;

public record Competition
{
    public Competition(string name, CompetitionRuleset ruleset, CompetitionType type)
    {
        Name = name;
        Ruleset = ruleset;
        Type = type;
    }

    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }
    public CompetitionType Type { get; }

    public override string ToString()
    {
        return $"{Name} ({Ruleset})";
    }
}
