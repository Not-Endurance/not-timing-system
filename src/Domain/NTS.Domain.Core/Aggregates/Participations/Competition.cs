namespace NTS.Domain.Core.Aggregates.Participations;

public class Competition : Entity
{
    public Competition(string name, CompetitionRuleset ruleset, CompetitionType type) : base(name, ruleset, type)
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
