using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NoTiming.Ui.Constants;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;

namespace NoTiming.Ui.Components.ParticipationTable.Phases;

public abstract class PhaseUpdateShellBehind : KrudShell<PhaseUpdateModel>
{
    protected PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);
}
