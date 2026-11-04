using NC.Structures;
using NoTiming.Domain.Setup.Services.StartValidation;

namespace NoTiming.Judge.Contracts.Features.Core;

public interface IDashService
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId);
    Task Start(int configureEventId);
    Task Deactivate();
    Task Reset();
}
