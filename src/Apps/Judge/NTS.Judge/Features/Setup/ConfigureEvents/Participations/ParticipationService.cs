using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Features.Setup.ConfigureEvents.Participations;

public class ParticipationService : KrudServiceBase<Participation, ParticipationFormModel>, ITransient
{
    public ParticipationService(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirrorService<Participation>> dependants
    )
        : base(participations, dependants) { }
}
