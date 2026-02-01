using Not.Application.CRUD.Ports;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionBehind : KrudServiceBase<Competition, CompetitionFormModel>
{
    //readonly IKrudParentNodeOf<Phase> _phaseParent;
    //readonly IKrudParentNodeOf<Participation> _participationParent;

    public CompetitionBehind(IRepository<Competition> competitions)
        : base(competitions, [])
    {
    }
}
