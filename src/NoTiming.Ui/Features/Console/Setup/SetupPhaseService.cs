using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Phases of a Competition of the Setup that is open (#646).</summary>
public class SetupPhaseService : KrudServiceBase<Phase, PhaseFormModel>, ITransient
{
    public SetupPhaseService(IRepository<Phase> phases, IEnumerable<IKrudMirrorService<Phase>> reflections)
        : base(phases, reflections) { }
}
