using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>The Athletes of the Tenant's registry (#646, ADR-0010): the list and the form that the Krud pages of the registry use.</summary>
public class RegistryAthleteService : KrudServiceBase<Athlete, AthleteFormModel>, ITransient
{
    public RegistryAthleteService(IRepository<Athlete> athletes, IEnumerable<IKrudMirrorService<Athlete>> dependants)
        : base(athletes, dependants) { }
}
