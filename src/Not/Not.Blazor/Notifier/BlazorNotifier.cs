using MudBlazor;
using Not.Blazor.Mud;
using Not.Notify;

namespace Not.Blazor.Notifier;

public class BlazorNotifier : ComponentBase
{
    readonly TimeSpan _failedDuration = TimeSpan.FromSeconds(30);

    public BlazorNotifier()
    {
        NotificationEvents.Informed.SubscribeAsync(AddSnack);
        NotificationEvents.Succeded.SubscribeAsync(AddSnack);
        NotificationEvents.Warned.SubscribeAsync(AddSnack);
        NotificationEvents.Failed.SubscribeAsync(AddSnack);
    }

    [Inject]
    ISnackbar Snackbar { get; set; } = default!;

    void AddSnack(Information informed)
    {
        Snackbar.Add(informed.Message, Severity.Info);
    }

    void AddSnack(Warning warned)
    {
        Snackbar.Add(warned.Message, Severity.Warning);
    }

    void AddSnack(Failure failed)
    {
        Snackbar.Add(failed.Message, Severity.Error, config => config.SetVisibleDuration(_failedDuration));
    }

    void AddSnack(Success succeeded)
    {
        Snackbar.Add(succeeded.Message, Severity.Success);
    }
}
