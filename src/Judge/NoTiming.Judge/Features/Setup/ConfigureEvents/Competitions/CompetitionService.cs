using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Competitions;

public class CompetitionService : KrudServiceBase<Competition, CompetitionFormModel>, ITransient
{
    public CompetitionService(IRepository<Competition> competitions)
        : base(competitions, []) { }
}
