using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal class ConfiguredCompetitionsValidationRule : IStartValidationRule
{
    public IEnumerable<StartValidationIssue> Evaluate(ConfigureEvent setupEvent)
    {
        if (setupEvent.Competitions.Count == 0)
        {
            yield return new StartValidationIssue("At least one competition must be configured before start.");
        }
    }
}
