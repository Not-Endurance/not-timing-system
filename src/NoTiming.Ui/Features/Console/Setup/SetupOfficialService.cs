using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Officials of the Setup that is open, as the Results name them (#646). Linking an account is the grants' business.</summary>
public class SetupOfficialService : KrudServiceBase<Official, OfficialFormModel>, ITransient
{
    public SetupOfficialService(IRepository<Official> officials, IEnumerable<IKrudMirrorService<Official>> dependants)
        : base(officials, dependants) { }
}
