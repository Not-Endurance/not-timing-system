using NC.Application.Behinds.Adapters;

namespace NoTiming.Judge.Contracts.Features.Core.Dashboard;

public interface IInspectionService : IStatefulService
{
    bool IsRepresentRequested { get; }
    bool IsInspectionRequested { get; }
    Task RequestRepresent(bool requestFlag);
    Task RequestInspection(bool requestFlag);
}
