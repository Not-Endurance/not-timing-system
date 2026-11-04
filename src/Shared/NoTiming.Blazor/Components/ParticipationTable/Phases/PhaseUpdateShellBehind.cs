using MudBlazor;
using NC.Krud.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Blazor.Constants;

namespace NoTiming.Blazor.Components.ParticipationTable.Phases;

public abstract class PhaseUpdateShellBehind : KrudShell<PhaseUpdateModel>
{
    protected PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);
}
