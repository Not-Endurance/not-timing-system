using MudBlazor;
using NTS.Blazor.Constants;
using NTS.Domain.Watcher;

namespace NTS.Witness.Components.Pages.Snapshot;

public partial class TimestampForm
{
    static readonly PatternMask TIME_MASK = new(Masks.SECONDS_TIME_MASK_FORMAT);

    MudTextField<string?> _timestampField = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(SnapshotParticipant), () => _timestampField);
    }
}
