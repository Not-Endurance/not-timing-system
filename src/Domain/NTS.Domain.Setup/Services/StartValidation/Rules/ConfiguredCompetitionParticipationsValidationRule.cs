using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal class ConfiguredCompetitionParticipationsValidationRule : IStartValidationRule
{
    public IEnumerable<StartValidationIssue> Evaluate(UpcomingEvent setupEvent)
    {
        foreach (var competition in setupEvent.Competitions.Where(x => x.Participations.Count == 0))
        {
            yield return new StartValidationIssue(
                $"Competition '{competition.Name}' must have at least one participation before start."
            );
        }
    }
}
