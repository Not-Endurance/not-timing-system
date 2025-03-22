using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Archive.Models;

public class CompetitionDocumentModel
{
    public CompetitionDocumentModel(Competition domainModel)
    {
        Name = domainModel.Name;
        Ruleset = domainModel.Ruleset;
        Type = domainModel.Type;
    }

    public string Name { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
}
