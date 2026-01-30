using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Judge.Features.Setup.UpcomingEvents.Officials;

public class OfficialBehind : KrudServiceBase<Official, OfficialFormModel>
{
    public OfficialBehind(IRepository<Official> officials, IEnumerable<IKrudMirror<Official>> dependants)
        : base(officials, dependants) { }

    protected override Official CreateEntity(OfficialFormModel model)
    {
        var names = ConvertName(model.Name);
        return new Official(names, model.Role, model.Id);
    }

    Person? ConvertName(string? combined)
    {
        return combined == null ? null : new Person(combined.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }
}
