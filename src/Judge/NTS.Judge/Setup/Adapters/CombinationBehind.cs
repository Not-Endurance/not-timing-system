using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Combinations.Dot;

namespace NTS.Judge.Setup.Adapters;

public class CombinationBehind
    : CrudBehind<Combination, CombinationFormModel>,
        ICrudReflection<Athlete>,
        ICrudReflection<Horse>
{
    public CombinationBehind(IRepository<Combination> repository, IEnumerable<ICrudReflection<Combination>> dependants)
        : base(repository, dependants) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }

    public Task Reflect(Athlete athlete)
    {
        UpdateReflections(x => x.Athlete, athlete, x => x.Reflect(athlete));
        return Task.CompletedTask;
    }

    public Task Reflect(Horse horse)
    {
        UpdateReflections(x => x.Horse, horse, x => x.Reflect(horse));
        return Task.CompletedTask;
    }
}
