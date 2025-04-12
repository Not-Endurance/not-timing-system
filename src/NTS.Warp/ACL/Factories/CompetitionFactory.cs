using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Relay.ACL.Entities.Competitions;
using NTS.Relay.ACL.Models;

namespace NTS.Relay.ACL.Factories;

public class CompetitionFactory
{
    public static EmsCompetition Create(Participation participation)
    {
        var laps = LapFactory.Create(participation);
        var state = new EmsCompetitionState
        {
            Id = participation.Id,
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

    public static EmsCompetitionType MapEmsCompetitionType(CompetitionRuleset ruleset)
    {
        return ruleset switch
        {
            CompetitionRuleset.Regional => EmsCompetitionType.National,
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
