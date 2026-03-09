using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Services.StartValidation.Rules;

internal interface IStartValidationRule
{
    IEnumerable<StartValidationIssue> Evaluate(UpcomingEvent setupEvent);
}
