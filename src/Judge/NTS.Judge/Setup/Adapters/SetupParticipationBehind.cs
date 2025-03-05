using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.Ports;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Contestants;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class SetupParticipationBehind : CrudBehind<Participation, ParticipationFormModel>
{
    public SetupParticipationBehind(
        IRepository<Participation> participations,
        IEnumerable<ISingleParentContext> singleParentContexts,
        CompetitionParentContext parentContext
    )
        : base(participations, singleParentContexts, parentContext) { }

    protected override Participation CreateEntity(ParticipationFormModel model)
    {
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Create(
            newStart,
            model.IsNotRanked,
            model.Combination,
            model.MaxSpeedOverride
        );
    }

    protected override Participation UpdateEntity(ParticipationFormModel model)
    {
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Update(
            model.Id,
            newStart,
            model.IsNotRanked,
            model.Combination,
            model.MaxSpeedOverride
        );
    }
}
