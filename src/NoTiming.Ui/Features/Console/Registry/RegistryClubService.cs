using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>The Clubs of the Tenant's registry (#646, ADR-0010).</summary>
public class RegistryClubService : KrudServiceBase<Club, ClubFormModel>, ITransient
{
    public RegistryClubService(IRepository<Club> clubs, IEnumerable<IKrudMirrorService<Club>> dependants)
        : base(clubs, dependants) { }
}
