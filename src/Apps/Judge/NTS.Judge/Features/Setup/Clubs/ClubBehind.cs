using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.Clubs;

internal class ClubBehind : KrudServiceBase<Club, ClubFormModel>
{
    public ClubBehind(IRepository<Club> repository, IEnumerable<IKrudMirror<Club>> reflections)
        : base(repository, reflections) { }

    protected override Club CreateEntity(ClubFormModel model)
    {
        return Club.Create(model.Name);
    }

    protected override Club UpdateEntity(ClubFormModel model)
    {
        return Club.Update(model.Id!.Value, model.Name);
    }
}
