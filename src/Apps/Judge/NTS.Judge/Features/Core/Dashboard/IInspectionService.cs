using Not.Application.Behinds.Adapters;

namespace NTS.Judge.Features.Core.Dashboard;

public interface IInspectionService : IStatefulService
{
    bool IsRepresentRequired { get; }
    bool IsRepresentRequested { get; }
    Task RequestRepresent(bool requestFlag);
    Task RequireRepresent(bool requestFlag);
}
