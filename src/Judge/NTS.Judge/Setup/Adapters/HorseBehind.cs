using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.AthletesHorses.Horses;

namespace NTS.Judge.Setup.Adapters;

public class HorseBehind : CrudBehind<Horse, HorseFormModel>
{
    public HorseBehind(IRepository<Horse> repository, IEnumerable<ICrudReflection<Horse>> dependants)
        : base(repository, dependants)
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
