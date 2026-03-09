using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal class SimultaneousParticipationInDifferantTracksValidationRule : IStartValidationRule
{
    public IEnumerable<StartValidationIssue> Evaluate(UpcomingEvent setupEvent)
    {
        var byParticipation = setupEvent
            .Competitions
            .SelectMany(
                (competition, competitionIndex) =>
                    competition.Participations.Select(participation => new
                    {
                        Participation = participation,
                        ParticipationNumber = participation.Combination.Number,
                        Competition = competition,
                        CompetitionIndex = competitionIndex,
                    })
            )
            .GroupBy(x => x.ParticipationNumber);

        foreach (var group in byParticipation)
        {
            var competitions = group
                .GroupBy(x => x.CompetitionIndex)
                .Select(x => x.First().Competition)
                .ToList();

            if (competitions.Count < 2)
            {
                continue;
            }

            var signatures = competitions
                .Select(competition => new { Competition = competition, PhaseSignature = CreatePhaseSignature(competition.Phases) })
                .ToList();

            var baseline = signatures[0].PhaseSignature;
            var hasDifference = signatures.Skip(1).Any(x => !x.PhaseSignature.SequenceEqual(baseline));
            if (!hasDifference)
            {
                continue;
            }

            var competitionDetails = signatures
                .Select(x => new StartValidationCompetition(x.Competition.Id, x.Competition.Name, FormatPhaseSignature(x.PhaseSignature)))
                .ToList();

            var combination = group.First().Participation.Combination;
            var athlete = string.Join(' ', combination.Athlete.Names);
            var horse = combination.Horse.Name;
            yield return new StartValidationIssue(group.Key, athlete, horse, competitionDetails);
        }
    }

    static IReadOnlyList<(decimal Distance, int Recovery, int? RestMinutes)> CreatePhaseSignature(IReadOnlyList<Phase> phases)
    {
        return phases
            .Select(x => ((decimal)x.Loop.Distance, x.Recovery, x.Rest))
            .ToList();
    }

    static string FormatPhaseSignature(IReadOnlyList<(decimal Distance, int Recovery, int? RestMinutes)> phases)
    {
        return string.Join(" | ", phases.Select(FormatPhase));

        static string FormatPhase((decimal Distance, int Recovery, int? RestMinutes) phase)
        {
            var rest = phase.RestMinutes.HasValue ? $"{phase.RestMinutes.Value}min" : "final";
            return $"{phase.Distance:0.##}km/{phase.Recovery}min/{rest}";
        }
    }
}
