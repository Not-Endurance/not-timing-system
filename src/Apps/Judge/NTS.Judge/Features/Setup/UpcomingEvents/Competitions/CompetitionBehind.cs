using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionBehind : KrudServiceBase<Competition, CompetitionFormModel>, ITransient
{
    public CompetitionBehind(IRepository<Competition> competitions)
        : base(competitions, [])
    {
    }
}
