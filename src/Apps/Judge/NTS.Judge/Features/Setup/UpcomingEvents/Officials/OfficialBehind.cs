using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialBehind : CrudChildBehind<Official, OfficialFormModel>
{
    public OfficialBehind(
        UpcomingEventCrudeContext upcomingEventContext,
        IEnumerable<ICrudReflection<Official>> dependants
    )
        : base(dependants, upcomingEventContext) { }

    protected override Official CreateEntity(OfficialFormModel model)
    {
        return Official.Create(model.Name, model.Role);
    }

    protected override Official UpdateEntity(OfficialFormModel model)
    {
        return Official.Update(model.Id, model.Name, model.Role);
    }
}
