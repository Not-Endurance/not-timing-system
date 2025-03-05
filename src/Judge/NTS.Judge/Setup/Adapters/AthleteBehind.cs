using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

namespace NTS.Judge.Setup.Adapters;

public class AthleteBehind : CrudBehind<Athlete, AthleteFormModel>
{
    public AthleteBehind(IRepository<Athlete> repository, IEnumerable<ISingleParentContext> singleParentContexts)
        : base(repository, singleParentContexts) { }

    protected override Athlete CreateEntity(AthleteFormModel model)
    {
        return Athlete.Create(model.Name, model.FeiId, model.Country, model.Club, model.Category);
    }

    protected override Athlete UpdateEntity(AthleteFormModel model)
    {
        return Athlete.Update(
            model.Id,
            model.Name,
            model.FeiId,
            model.Country,
            model.Club,
            model.Category
        );
    }
}
