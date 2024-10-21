using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Athletes;

namespace NTS.Judge.Events;

public class AthleteBehind : CrudBehind<Athlete, AthleteFormModel>
{
    public AthleteBehind(IRepository<Athlete> repository) : base(repository)
    {
    }

    protected override Athlete CreateEntity(AthleteFormModel model)
    {
        return Athlete.Create(model.Name, model.FeiId, model.Country, model.Club, model.Category);
    }

    protected override Athlete UpdateEntity(AthleteFormModel model)
    {
        return Athlete.Update(model.Id, model.Name, model.FeiId, model.Country, model.Club, model.Category);
    }
}
