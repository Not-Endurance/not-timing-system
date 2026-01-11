using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Athletes;

public class AthleteBehind : KrudServiceBase<Athlete, AthleteFormModel>, IKrudMirror<Club>
{
    readonly IRepository<Athlete> _repository;

    public AthleteBehind(IRepository<Athlete> repository, IEnumerable<IKrudMirror<Athlete>> dependants)
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
        var athlets = await ReadMany();
        foreach (var athlete in athlets.Where(x => x.Club == update))
        {
            athlete.Reflect(update);
            await _repository.Update(athlete);
        }
    }
}
