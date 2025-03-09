using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.AthletesHorses.Athletes;

namespace NTS.Judge.Setup.Adapters;

public class AthleteBehind : CrudBehind<Athlete, AthleteFormModel>, ICrudReflection<Club>
{
    public AthleteBehind(IRepository<Athlete> repository, IEnumerable<ICrudReflection<Athlete>> dependants)
        : base(repository, dependants) 
    {
    }

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

    public Task Reflect(Club dependable)
    {
        UpdateReflections(x => x.Club, dependable, athlete => athlete.Reflect(dependable));
        return Task.CompletedTask;
    }
}
