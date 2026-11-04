using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Judge.Features.Setup.Athletes;

public class AthleteService : KrudServiceBase<Athlete, AthleteFormModel>, ITransient
{
    public AthleteService(IRepository<Athlete> repository, IEnumerable<IKrudMirrorService<Athlete>> dependants)
        : base(repository, dependants) { }
}
