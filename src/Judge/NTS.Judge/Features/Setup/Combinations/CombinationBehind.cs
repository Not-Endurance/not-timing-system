using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Combinations.Dot;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Features.Setup.Combinations;

public class CombinationBehind : CrudChildBehind<Combination, CombinationFormModel>
{
    public CombinationBehind(
        IEnumerable<ICrudReflection<Combination>> reflections,
        UpcomingEventCrudeContext parentContext
    )
        : base(reflections, parentContext) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }
}
