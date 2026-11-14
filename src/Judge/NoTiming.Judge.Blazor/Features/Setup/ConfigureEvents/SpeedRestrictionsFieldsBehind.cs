using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Shared.Models;

namespace NoTiming.Judge.Blazor.Features.Setup.ConfigureEvents;

public abstract class SpeedRestrictionsFieldsBehind : NComponent
{
    SpeedRestrictionsModel? _loaded;

    // Each checkbox decides whether its group is shown. A group opens when it already has values.
    protected bool HasSpeedLimit { get; set; }

    protected bool HasAverageSpeedLimit { get; set; }

    [Parameter, EditorRequired]
    public SpeedRestrictionsModel Restrictions { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loaded, Restrictions))
        {
            return;
        }
        _loaded = Restrictions;
        HasSpeedLimit =
            Restrictions.LoopSpeedMin != null
            || Restrictions.LoopSpeedMax != null
            || Restrictions.PhaseSpeedMin != null
            || Restrictions.PhaseSpeedMax != null;
        HasAverageSpeedLimit =
            Restrictions.LoopAverageSpeedMin != null
            || Restrictions.LoopAverageSpeedMax != null
            || Restrictions.PhaseAverageSpeedMin != null
            || Restrictions.PhaseAverageSpeedMax != null;
    }

    // Unchecking clears the group, so a hidden field never keeps a value.
    protected void SetSpeedLimit(bool value)
    {
        HasSpeedLimit = value;
        if (!value)
        {
            Restrictions.LoopSpeedMin = null;
            Restrictions.LoopSpeedMax = null;
            Restrictions.PhaseSpeedMin = null;
            Restrictions.PhaseSpeedMax = null;
        }
    }

    protected void SetAverageSpeedLimit(bool value)
    {
        HasAverageSpeedLimit = value;
        if (!value)
        {
            Restrictions.LoopAverageSpeedMin = null;
            Restrictions.LoopAverageSpeedMax = null;
            Restrictions.PhaseAverageSpeedMin = null;
            Restrictions.PhaseAverageSpeedMax = null;
        }
    }
}
