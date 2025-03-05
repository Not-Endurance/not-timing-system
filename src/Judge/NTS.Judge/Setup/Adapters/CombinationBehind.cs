using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Combinations.Dot;

namespace NTS.Judge.Setup.Adapters;

public class CombinationBehind : CrudBehind<Combination, CombinationFormModel>, ISingleParentContext
{
    public CombinationBehind(IRepository<Combination> repository)
        : base(repository, []) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }

    public void Update(object child)
    {
        if (child is Athlete athlete)
        {
            foreach (var combination in Items)
            {
                if (combination.Athlete == athlete)
                {
                    combination.Update(athlete);
                }
            }
        }
        if (child is Horse horse)
        {
            foreach (var combination in Items)
            {
                if (combination.Horse == horse)
                {
                    combination.Update(horse);
                }
            }
        }
    }
}
