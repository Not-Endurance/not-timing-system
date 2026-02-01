using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Participations;

public class ParticipationBehind : KrudServiceBase<Participation, ParticipationFormModel>
{
    public ParticipationBehind(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirror<Participation>> dependants
    )
        : base(participations, dependants) { }
}
