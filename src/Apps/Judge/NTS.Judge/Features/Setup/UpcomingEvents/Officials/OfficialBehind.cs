using Not.Application.Krud;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialBehind : KrudService<Official, OfficialFormModel>
{
    public OfficialBehind(
        UpcomingEventKrudRoot upcomingEventContext,
        IEnumerable<IKrudMirror<Official>> dependants
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
