using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal class NonFinalPhasesMustHaveRestValidationRule : IStartValidationRule
{
    public IEnumerable<StartValidationIssue> Evaluate(ConfigureEvent setupEvent)
    {
        foreach (var competition in setupEvent.Competitions)
        {
            var invalidPhaseNumbers = competition
                .Phases.Take(Math.Max(competition.Phases.Count - 1, 0))
                .Select((phase, index) => new { phase, Number = index + 1 })
                .Where(x => x.phase.Rest == null)
                .Select(x => x.Number)
                .ToList();

            if (invalidPhaseNumbers.Count == 0)
            {
                continue;
            }

            yield return new StartValidationIssue(
                $"Competition '{competition.Name}' has non-final phases without rest: {string.Join(", ", invalidPhaseNumbers)}."
            );
        }
    }
}
