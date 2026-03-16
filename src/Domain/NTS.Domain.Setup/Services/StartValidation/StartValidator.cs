using Not.Structures;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation.Rules;

namespace NTS.Domain.Setup.Services.StartValidation;

public static class StartValidator
{
    static readonly IReadOnlyList<IStartValidationRule> RULES =
    [
        new SimultaneousParticipationInDifferantTracksValidationRule(),
    ];

    public static Result<IReadOnlyList<StartValidationIssue>> Validate(UpcomingEvent setupEvent)
    {
        IReadOnlyList<StartValidationIssue> issues = RULES
            .SelectMany(x => x.Evaluate(setupEvent))
            .ToList()
            .AsReadOnly();
        return Result.Success(issues);
    }
}
