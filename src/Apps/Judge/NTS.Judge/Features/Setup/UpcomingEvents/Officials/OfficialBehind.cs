using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Features.Core.Behinds;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialBehind : KrudServiceBase<Official, OfficialFormModel>
{
    public OfficialBehind(
        IRepository<Official> officials,
        IEnumerable<IKrudMirror<Official>> dependants
    )
        : base(officials, dependants) { }

    protected override Official CreateEntity(OfficialFormModel model)
    {
        return Official.Create(model.Name, model.Role);
    }

    protected override Official UpdateEntity(OfficialFormModel model)
    {
        return Official.Update(model.Id, model.Name, model.Role);
    }
}
