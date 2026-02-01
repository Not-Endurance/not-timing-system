using Not.Application.CRUD.Ports;
using Not.Krud.Abstractions;
using Not.Krud.Services;
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
