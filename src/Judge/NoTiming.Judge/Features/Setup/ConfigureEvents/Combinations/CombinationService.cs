using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Features.Setup.ConfigureEvents.Combinations;

public class CombinationService : KrudServiceBase<Combination, CombinationFormModel>, ITransient
{
    public CombinationService(
        IRepository<Combination> combinations,
        IEnumerable<IKrudMirrorService<Combination>> reflections
    )
        : base(combinations, reflections) { }
}
