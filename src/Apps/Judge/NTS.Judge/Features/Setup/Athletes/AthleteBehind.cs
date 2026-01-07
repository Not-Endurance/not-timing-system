using MudBlazor.Extensions;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteBehind : CrudBehind<Athlete, AthleteFormModel>, ICrudReflection<Club>
{
    readonly IRepository<Athlete> _repository;

    public AthleteBehind(IRepository<Athlete> repository, IEnumerable<ICrudReflection<Athlete>> dependants)
        : base(repository, dependants)
    {
        _repository = repository;
    }

    protected override Athlete CreateEntity(AthleteFormModel model)
    {
        return new Athlete(Person.Create(model.Name), model.FeiId, model.Country, model.Club);
    }

    protected override Athlete UpdateEntity(AthleteFormModel model)
    {
        return new Athlete(model.Id, Person.Create(model.Name), model.FeiId, model.Country, model.Club);
    }

    public async Task Reflect(Club update)
    {
        foreach (var athlete in Items.Where(x => x.Club == update))
        {
            athlete.Reflect(update);
            await _repository.Update(athlete);
        }
    }
}
