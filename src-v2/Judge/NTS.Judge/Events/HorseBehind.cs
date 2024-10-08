using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Horses;

namespace NTS.Judge.Events;

public class HorseBehind : SimpleCrudBehind<Horse, HorseFormModel>
{
    public HorseBehind(IRepository<Horse> repository) : base(repository)
    {
    }

    protected override Horse CreateEntity(HorseFormModel model)
    {
        return Horse.Create(model.Name, model.FeiId);
    }

    protected override Horse UpdateEntity(HorseFormModel model)
    {
        return Horse.Update(model.Id, model.Name, model.FeiId);
    }
}
