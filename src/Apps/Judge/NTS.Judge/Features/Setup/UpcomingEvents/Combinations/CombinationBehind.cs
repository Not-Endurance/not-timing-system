using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Combinations;

public class CombinationBehind : KrudServiceBase<Combination, CombinationFormModel>, ITransient
{
    public CombinationBehind(IRepository<Combination> combinations, IEnumerable<IKrudMirror<Combination>> reflections)
        : base(combinations, reflections) { }
}
