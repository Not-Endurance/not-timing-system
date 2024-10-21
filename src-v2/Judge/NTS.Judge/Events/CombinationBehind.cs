using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Combinations;

namespace NTS.Judge.Events;

public class CombinationBehind : CrudBehind<Combination, CombinationFormModel>
{
    public CombinationBehind(IRepository<Combination> repository) : base(repository)
    {
    }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }
}
