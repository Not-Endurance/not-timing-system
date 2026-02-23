using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Competitions;

public class CompetitionService : KrudServiceBase<Competition, CompetitionFormModel>, ITransient
{
    public CompetitionService(IRepository<Competition> competitions)
        : base(competitions, [])
    {
    }
}
