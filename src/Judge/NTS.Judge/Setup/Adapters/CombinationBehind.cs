using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Combinations.Dot;

namespace NTS.Judge.Setup.Adapters;

public class CombinationBehind
    : CrudBehind<Combination, CombinationFormModel>, ICrudDependant<Athlete>, ICrudDependant<Horse>
{
    public CombinationBehind(IRepository<Combination> repository)
        : base(repository) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }

    public void Update(Athlete athlete)
    {
        Items.FirstOrDefault(x => x.Athlete == athlete)?.Update(athlete);
    }

    public void Update(Horse horse)
    {
        Items.FirstOrDefault(x => x.Horse == horse)?.Update(horse);
    }
}
