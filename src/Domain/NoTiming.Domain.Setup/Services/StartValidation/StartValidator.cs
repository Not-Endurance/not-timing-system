using NC.Structures;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Services.StartValidation.Rules;

namespace NoTiming.Domain.Setup.Services.StartValidation;

public static class StartValidator
{
    static readonly IReadOnlyList<IStartValidationRule> RULES =
    [
        new ConfiguredCompetitionsValidationRule(),
        new ConfiguredCompetitionPhasesValidationRule(),
        new ConfiguredCompetitionParticipationsValidationRule(),
        new NonFinalPhasesMustHaveRestValidationRule(),
        new SimultaneousParticipationInDifferantTracksValidationRule(),
    ];

    public static Result<IReadOnlyList<StartValidationIssue>> Validate(ConfigureEvent setupEvent)
    {
        IReadOnlyList<StartValidationIssue> issues = RULES
            .SelectMany(x => x.Evaluate(setupEvent))
            .ToList()
            .AsReadOnly();
        return Result.Success(issues);
    }
}
