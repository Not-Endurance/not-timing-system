using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
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
        ValidateEntity(model);
        var newStart = model.IsStartTimeOverriden ? model.StartTimeOverride?.ToDateTimeOffset() : null;
        return Participation.Create(newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    protected override Participation UpdateEntity(ParticipationFormModel model)
    {
        ValidateEntity(model);
        var newStart = model.IsStartTimeOverriden ? model.StartTimeOverride?.ToDateTimeOffset() : null;
        return Participation.Update(model.Id, newStart, model.IsNotRanked, model.Combination, model.MaxSpeedOverride);
    }

    public void ValidateEntity(ParticipationFormModel model)
    {
        if (model.IsStartTimeOverriden && model.StartTimeOverride == null)
        {
            throw new DomainPropertyException(nameof(model.StartTimeOverride), Null_or_malformed_string, Start_Time_string);
        }
        if (model.IsMaxSpeedOverriden && model.MaxSpeedOverride == null)
        {
            throw new DomainPropertyException(nameof(model.MaxSpeedOverride), Null_or_malformed_string, Max_Speed_Penalty_string);
        }
    }

    // public async Task Reflect(Combination update)
    // {
    //     await UpdateReflections(x => x.Combination, update, x => x.Reflect(update));
    // }
}
