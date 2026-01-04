namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Inspections;

public interface IInspections : IParticipationContext
{
    Task RequestRepresent(bool requestFlag);
    Task RequireInspection(bool requestFlag);
}
