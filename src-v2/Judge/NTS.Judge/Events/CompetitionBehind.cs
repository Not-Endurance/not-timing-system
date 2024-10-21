using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Contexts;
using NTS.Judge.Setup.Competitions;

namespace NTS.Judge.Events;

public class CompetitionBehind : CrudBehind<Competition, CompetitionFormModel>
{
    private readonly EventParentContext _parentContext;

    public CompetitionBehind(IRepository<Competition> competitions, EventParentContext parentContext) : base(competitions, parentContext)
    {
        this._parentContext = parentContext;
    }

    protected override Competition CreateEntity(CompetitionFormModel model)
    {
        return Competition.Create(model.Name, model.Type, model.StartTime, model.CompulsoryThresholdMinutes);
    }

    protected override Competition UpdateEntity(CompetitionFormModel model)
    {
        return Competition.Update(
            model.Id,
            model.Name,
            model.Type,
            model.StartTime,
            model.CompulsoryThresholdMinutes,
            model.Phases,
            model.Participations);
    }
}
