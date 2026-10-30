using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Combinations of an Athlete and a Horse of the Setup that is open (#646).</summary>
public class SetupCombinationService : KrudServiceBase<Combination, CombinationFormModel>, ITransient
{
    public SetupCombinationService(
        IRepository<Combination> combinations,
        IEnumerable<IKrudMirrorService<Combination>> reflections
    )
        : base(combinations, reflections) { }
}
