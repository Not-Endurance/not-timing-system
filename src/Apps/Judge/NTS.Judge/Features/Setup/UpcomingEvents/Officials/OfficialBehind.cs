using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialBehind : KrudServiceBase<Official, OfficialFormModel>
{
    public OfficialBehind(IRepository<Official> officials, IEnumerable<IKrudMirror<Official>> dependants)
        : base(officials, dependants) { }
}
