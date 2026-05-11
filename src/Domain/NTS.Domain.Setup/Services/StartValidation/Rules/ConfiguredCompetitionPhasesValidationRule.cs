using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal class ConfiguredCompetitionPhasesValidationRule : IStartValidationRule
{
    public IEnumerable<StartValidationIssue> Evaluate(ConfigureEvent setupEvent)
    {
        foreach (var competition in setupEvent.Competitions.Where(x => x.Phases.Count == 0))
        {
            yield return new StartValidationIssue(
                $"Competition '{competition.Name}' must have at least one phase before start."
            );
        }
    }
}
