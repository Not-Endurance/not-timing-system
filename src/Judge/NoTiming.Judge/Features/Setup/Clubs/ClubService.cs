using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Judge.Features.Setup.Clubs;

internal class ClubService : KrudServiceBase<Club, ClubFormModel>, ITransient
{
    public ClubService(IRepository<Club> repository, IEnumerable<IKrudMirrorService<Club>> reflections)
        : base(repository, reflections) { }
}
