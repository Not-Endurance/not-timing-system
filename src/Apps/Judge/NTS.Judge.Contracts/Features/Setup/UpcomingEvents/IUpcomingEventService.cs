using Not.Injection;
using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Contracts.Features.Setup.UpcomingEvents;

public interface IUpcomingEventService : ITransient
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId);
    Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId);
}
