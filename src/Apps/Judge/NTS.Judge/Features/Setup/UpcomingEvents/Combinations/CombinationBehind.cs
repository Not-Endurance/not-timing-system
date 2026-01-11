using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationBehind : KrudService<Combination, CombinationFormModel>
{
    public CombinationBehind(
        IEnumerable<IKrudMirror<Combination>> reflections,
        IKrudParentNodeOf<Combination> parentNode
    )
        : base(reflections, parentNode) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return Combination.Create(model.Number, model.Athlete, model.Horse, model.Tag);
    }

    protected override Combination UpdateEntity(CombinationFormModel model)
    {
        return Combination.Update(model.Id, model.Number, model.Athlete, model.Horse, model.Tag);
    }
}
