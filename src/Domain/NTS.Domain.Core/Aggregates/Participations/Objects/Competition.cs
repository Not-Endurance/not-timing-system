namespace NTS.Domain.Core.Aggregates.Participations.Objects;

public record Competition
{
    public Competition(string name, CompetitionRuleset ruleset)
    {
        Name = name;
        Ruleset = ruleset;
    }

    public string Name { get; }
    public CompetitionRuleset Ruleset { get; }

    public override string ToString()
    {
        return $"{Name} ({Ruleset})";
    }
}
