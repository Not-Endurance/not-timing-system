using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Officials;

public class OfficialService : KrudServiceBase<Official, OfficialFormModel>, ITransient
{
    public OfficialService(IRepository<Official> officials, IEnumerable<IKrudMirrorService<Official>> dependants)
        : base(officials, dependants) { }
}
