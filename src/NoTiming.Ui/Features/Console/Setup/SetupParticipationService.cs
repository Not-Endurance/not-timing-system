using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Participations of a Competition of the Setup that is open (#646).</summary>
public class SetupParticipationService : KrudServiceBase<Participation, ParticipationFormModel>, ITransient
{
    public SetupParticipationService(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirrorService<Participation>> dependants
    )
        : base(participations, dependants) { }
}
