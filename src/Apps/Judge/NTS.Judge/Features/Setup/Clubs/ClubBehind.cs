using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Clubs;

internal class ClubBehind : KrudServiceBase<Club, ClubFormModel>, ITransient
{
    public ClubBehind(IRepository<Club> repository, IEnumerable<IKrudMirror<Club>> reflections)
        : base(repository, reflections) { }
}
