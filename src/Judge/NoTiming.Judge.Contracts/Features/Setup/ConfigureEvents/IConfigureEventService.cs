using NC.Injection;
using NC.Structures;
using NoTiming.Domain.Setup.Services.StartValidation;

namespace NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents;

public interface IConfigureEventService : ITransient
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId);
    Task DeleteParticipation(int configureEventId, int participationNumber, int competitionId);
}
