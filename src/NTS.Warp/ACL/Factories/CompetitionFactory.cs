using Not.Extensions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Warp.ACL.Entities.Competitions;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class CompetitionFactory
{
    public static EmsCompetition Create(ParticipationModel participation)
    {
        var laps = LapFactory.Create(participation.Phases);
        var state = new EmsCompetitionState
        {
            Id = DomainModelHelper.GenerateId(),
            Name = participation.Competition.Name,
            Type = MapEmsCompetitionType(participation.Competition.Ruleset),
        };
        var competition = new EmsCompetition(state);
        foreach (var lap in laps)
        {
            competition.Save(lap);
        }
        return competition;
    }

    public static EmsCompetitionType MapEmsCompetitionType(CompetitionRuleset? ruleset)
    {
        return ruleset switch
        {
            null or CompetitionRuleset.Regional => EmsCompetitionType.National,
            CompetitionRuleset.FEI => EmsCompetitionType.International,
            _ => throw new NotImplementedException(),
        };
    }

    public static CompetitionRuleset MapCompetitionRuleset(EmsCompetitionType emsCompetitionType)
    {
        return emsCompetitionType switch
        {
            EmsCompetitionType.National => CompetitionRuleset.Regional,
            EmsCompetitionType.International => CompetitionRuleset.FEI,
            _ => throw new NotImplementedException(),
        };
    }
}
