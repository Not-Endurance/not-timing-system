using Not.Injection;
using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents;

public interface IConfigureEventService : ITransient
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId);
    Task DeleteParticipation(int configureEventId, int participationNumber, int competitionId);
}
