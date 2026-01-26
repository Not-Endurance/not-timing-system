using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationBehind : KrudServiceBase<Combination, CombinationFormModel>
{
    public CombinationBehind(IRepository<Combination> combinations, IEnumerable<IKrudMirror<Combination>> reflections)
        : base(combinations, reflections) { }

    protected override Combination CreateEntity(CombinationFormModel model)
    {
        return new(model.Number, model.Athlete, model.Horse, model.Id);
    }
}
