using MudBlazor;
using Not.Blazor.Mud.Extensions;
using Not.Notifier;

namespace Not.Blazor.Notifier;

public class BlazorNotifier : ComponentBase
{
    private readonly TimeSpan _failedDuration = TimeSpan.FromSeconds(30);

    [Inject]
    ISnackbar _snackbar { get; set; } = default!;

    public BlazorNotifier()
    {
        Notifications.InformedEvent.SubscribeAsync(AddSnack);
        Notifications.SucceededEvent.SubscribeAsync(AddSnack);
        Notifications.WarnedEvent.SubscribeAsync(AddSnack);
        Notifications.FailedEvent.SubscribeAsync(AddSnack);
    }

    void AddSnack(Informed informed)
    {
        _snackbar.Add(informed.Message, Severity.Info);
    }

    void AddSnack(Warned warned)
    {
        _snackbar.Add(warned.Message, Severity.Warning);
    }

    void AddSnack(Failed failed)
    {
        _snackbar.Add(failed.Message, Severity.Error, config => config.SetVisibleDuration(_failedDuration));
    }

    void AddSnack(Succeeded succeeded)
    {
        _snackbar.Add(succeeded.Message, Severity.Success);
    }
}
