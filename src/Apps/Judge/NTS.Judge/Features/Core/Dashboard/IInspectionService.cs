namespace NTS.Judge.Features.Core.Dashboard;

public interface IInspectionService : IParticipationContext
{
    Task RequestRepresent(bool requestFlag);
    Task RequireRepresent(bool requestFlag);
}
