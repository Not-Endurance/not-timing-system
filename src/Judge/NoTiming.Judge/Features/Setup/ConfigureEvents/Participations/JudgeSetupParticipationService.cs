using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Participations;

public class JudgeSetupParticipationService : KrudServiceBase<Participation, ParticipationFormModel>, ITransient
{
    public JudgeSetupParticipationService(
        IRepository<Participation> participations,
        IEnumerable<IKrudMirrorService<Participation>> dependants
    )
        : base(participations, dependants) { }
}
