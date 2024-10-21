using AngleSharp.Io;
using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Setup.Officials;
using NTS.Judge.Contexts;

namespace NTS.Judge.Events;

public class OfficialBehind : CrudBehind<Official, OfficialFormModel>
{
    public OfficialBehind(IRepository<Official> official, EventParentContext enduraceEventContext) : base(official, enduraceEventContext)
    {
    }

    protected override Official CreateEntity(OfficialFormModel model)
    {
        return Official.Create(model.Name, model.Role);
    }

    protected override Official UpdateEntity(OfficialFormModel model)
    {
        return Official.Update(model.Id, model.Name, model.Role);
    }
}
