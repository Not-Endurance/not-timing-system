using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Contracts.Core.Models;
using NTS.Blazor.Constants;

namespace NTS.Blazor.Components.ParticipationTable.Phases;

public abstract class PhaseUpdateShellBehind : KrudShell<PhaseUpdateModel>
{
    protected PatternMask TimeMask { get; } = new(Masks.SECONDS_TIME_MASK_FORMAT);
}
