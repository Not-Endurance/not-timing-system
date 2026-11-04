using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Domain.Setup.Services.StartValidation.Rules;

internal interface IStartValidationRule
{
    IEnumerable<StartValidationIssue> Evaluate(ConfigureEvent setupEvent);
}
