using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Combinations;

public class CombinationService : KrudServiceBase<Combination, CombinationFormModel>, ITransient
{
    public CombinationService(
        IRepository<Combination> combinations,
        IEnumerable<IKrudMirrorService<Combination>> reflections
    )
        : base(combinations, reflections) { }
}
