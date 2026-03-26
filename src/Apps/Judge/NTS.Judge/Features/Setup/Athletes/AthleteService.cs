using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteService : KrudServiceBase<Athlete, AthleteFormModel>, ITransient
{
    public AthleteService(IRepository<Athlete> repository, IEnumerable<IKrudMirror<Athlete>> dependants)
        : base(repository, dependants) { }
}
