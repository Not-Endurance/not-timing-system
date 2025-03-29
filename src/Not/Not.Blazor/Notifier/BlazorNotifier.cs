using MudBlazor;
using Not.Blazor.Mud;
using Not.Notify;

namespace Not.Blazor.Notifier;

public class BlazorNotifier : ComponentBase
{
    readonly TimeSpan _failedDuration = TimeSpan.FromSeconds(30);

    public BlazorNotifier()
    {
        NotificationEvents.INFORMED.SubscribeAsync(AddInformationSnack);
        NotificationEvents.SUCCEDED.SubscribeAsync(AddSuccessSnack);
        NotificationEvents.WARNED.SubscribeAsync(AddWarningSnack);
        NotificationEvents.FAILED.SubscribeAsync(AddFailureSnak);
    }

    [Inject]
    ISnackbar Snackbar { get; set; } = default!;

    void AddInformationSnack(string message)
    {
        Snackbar.Add(message, Severity.Info);
    }

    void AddWarningSnack(string message)
    {
        Snackbar.Add(message, Severity.Warning);
    }

    void AddFailureSnak(string message)
    {
        Snackbar.Add(message, Severity.Error, config => config.SetVisibleDuration(_failedDuration));
    }

    void AddSuccessSnack(string message)
    {
        Snackbar.Add(message, Severity.Success);
    }
}
