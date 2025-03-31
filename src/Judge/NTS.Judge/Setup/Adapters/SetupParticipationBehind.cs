using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Extensions;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents.Participations;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class SetupParticipationBehind
    : CrudChildBehind<Participation, ParticipationFormModel>,
        ICrudReflection<Combination>
{
    public SetupParticipationBehind(
        IRepository<Participation> participations,
        CompetitionParentContext parentContext,
        IEnumerable<ICrudReflection<Participation>> dependants
    )
        : base(participations, dependants, parentContext) { }

    protected override Participation CreateEntity(ParticipationFormModel model)
    {
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Create(newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    protected override Participation UpdateEntity(ParticipationFormModel model)
    {
        var newStart = model.StartTimeOverride?.ToDateTimeOffset();
        return Participation.Update(model.Id, newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    public Task Reflect(Combination combination)
    {
        UpdateReflections(x => x.Combination, combination, x => x.Reflect(combination));
        return Task.CompletedTask;
    }
}
