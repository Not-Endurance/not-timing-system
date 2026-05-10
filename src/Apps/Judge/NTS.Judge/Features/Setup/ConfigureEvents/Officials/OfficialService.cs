using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Features.Setup.ConfigureEvents.Officials;

public class OfficialService : KrudServiceBase<Official, OfficialFormModel>, ITransient
{
    public OfficialService(IRepository<Official> officials, IEnumerable<IKrudMirrorService<Official>> dependants)
        : base(officials, dependants) { }
}
