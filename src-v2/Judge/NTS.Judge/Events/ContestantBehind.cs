using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using Not.Extensions;
using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Pages.Setup.Contestants;
using NTS.Judge.Contexts;

namespace NTS.Judge.Events;

public class ContestantBehind : CrudBehind<Contestant, ContestantFormModel>
{
    public ContestantBehind(IRepository<Contestant> participations, CompetitionParentContext parentContext)
        : base(participations, parentContext)
    {
    }

    protected override Contestant CreateEntity(ContestantFormModel model)
    {
        return Contestant.Create(model.StartTimeOverride?.ToDateTimeOffset(), model.IsUnranked, model.Combination);
    }

    protected override Contestant UpdateEntity(ContestantFormModel model)
    {
        return Contestant.Update(model.Id, model.StartTimeOverride?.ToDateTimeOffset(), model.IsUnranked, model.Combination);
    }
}
