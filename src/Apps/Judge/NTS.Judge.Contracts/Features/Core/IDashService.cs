using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Judge.Contracts.Features.Core;

public interface IDashService
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId);
    Task Start(int configureEventId);
    Task Reset();
}
