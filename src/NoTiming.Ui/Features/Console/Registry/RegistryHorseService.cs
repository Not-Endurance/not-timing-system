using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>The Horses of the Tenant's registry (#646, ADR-0010).</summary>
public class RegistryHorseService : KrudServiceBase<Horse, HorseFormModel>, ITransient
{
    public RegistryHorseService(IRepository<Horse> horses, IEnumerable<IKrudMirrorService<Horse>> dependants)
        : base(horses, dependants) { }
}
