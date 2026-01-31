using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.AthletesHorses.Horses;

namespace NTS.Judge.Features.Setup.Horses;

public class HorseBehind : KrudServiceBase<Horse, HorseFormModel>
{
    public HorseBehind(IRepository<Horse> repository, IEnumerable<IKrudMirror<Horse>> dependants)
        : base(repository, dependants) { }

    protected override Horse CreateEntity(HorseFormModel model)
    {
        return new(model.Name, model.FeiId, model.Id);
    }
}
