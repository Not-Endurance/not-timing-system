using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Horses;

public class HorseService : KrudServiceBase<Horse, HorseFormModel>, ITransient
{
    public HorseService(IRepository<Horse> repository, IEnumerable<IKrudMirror<Horse>> dependants)
        : base(repository, dependants) { }
}
