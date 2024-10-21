namespace NTS.Domain.Core.Entities.ParticipationAggregate;

public record Competition : DomainObject
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
