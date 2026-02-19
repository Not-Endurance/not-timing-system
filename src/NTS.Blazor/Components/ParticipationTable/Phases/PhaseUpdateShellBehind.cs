using MudBlazor;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Application.Core;
using NTS.Blazor.Constants;

namespace NTS.Blazor.Components.ParticipationTable.Phases;

public abstract class PhaseUpdateShellBehind : KrudShell<PhaseUpdateModel>
{
    protected readonly PatternMask TIME_MASK = new(Masks.SECONDS_TIME_MASK_FORMAT);
}
