using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Judge.Features.Setup.Horses;

public class HorseService : KrudServiceBase<Horse, HorseFormModel>, ITransient
{
    public HorseService(IRepository<Horse> repository, IEnumerable<IKrudMirrorService<Horse>> dependants)
        : base(repository, dependants) { }
}
