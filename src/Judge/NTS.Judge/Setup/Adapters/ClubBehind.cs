using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Clubs;

namespace NTS.Judge.Setup.Adapters;

internal class ClubBehind : CrudBehind<Club, ClubFormModel>
{
    public ClubBehind(IRepository<Club> repository, IEnumerable<ICrudReflection<Club>> reflections) : base(repository, reflections)
    {
    }

    protected override Club CreateEntity(ClubFormModel model)
    {
        return Club.Create(model.Name);
    }

    protected override Club UpdateEntity(ClubFormModel model)
    {
        return Club.Update(model.Id!.Value, model.Name);
    }
}
