using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Features.Setup.Participations;

public class SetupParticipationBehind : CrudChildBehind<Participation, ParticipationFormModel>
{
    public SetupParticipationBehind(
        CompetitionCrudeContext crudeContext,
        IEnumerable<ICrudReflection<Participation>> dependants
    )
        : base(dependants, crudeContext) { }

    protected override Participation CreateEntity(ParticipationFormModel model)
    {
        model.ValidateConditionalInputs();
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Create(newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    protected override Participation UpdateEntity(ParticipationFormModel model)
    {
        model.ValidateConditionalInputs();
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Update(model.Id, newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    // public async Task Reflect(Combination update)
    // {
    //     await UpdateReflections(x => x.Combination, update, x => x.Reflect(update));
    // }
}
