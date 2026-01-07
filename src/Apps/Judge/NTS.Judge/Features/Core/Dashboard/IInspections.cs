namespace NTS.Judge.Features.Core.Dashboard;

public interface IInspections : IParticipationContext
{
    Task RequestRepresent(bool requestFlag);
    Task RequireInspection(bool requestFlag);
}
