using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class ParticipationService : KrudServiceBase<Participation, ParticipationFormModel>, ITransient
{
    public ParticipationService(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirror<Participation>> dependants
    )
        : base(participations, dependants) { }
}
