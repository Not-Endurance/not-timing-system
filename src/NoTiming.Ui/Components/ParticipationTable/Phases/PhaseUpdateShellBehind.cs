using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;
using NoTiming.Ui.Constants;

namespace NoTiming.Ui.Components.ParticipationTable.Phases;

public abstract class PhaseUpdateShellBehind : KrudShell<PhaseUpdateModel>
{
    protected PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);
}
