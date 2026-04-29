using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Contracts.Features.Core;

public interface IDashService
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId);
    Task Start(int upcomingEventId);
    Task Reset();
}
