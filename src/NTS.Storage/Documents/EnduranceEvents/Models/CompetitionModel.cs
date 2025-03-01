using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class CompetitionModel
{
    public CompetitionModel(Competition domainModel)
    {
        Name = domainModel.Name;
        Ruleset = domainModel.Ruleset;
        Type = domainModel.Type;
    }

    public string Name { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
}
