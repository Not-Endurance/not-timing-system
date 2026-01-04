using System.Text;
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
        NotificationEvents.FAILED.SubscribeAsync(AddFailureSnack);
    }

    [Inject]
    ISnackbar Snackbar { get; set; } = default!;

    void AddInformationSnack(string message)
    {
        Snackbar.Add(FormatMessage(message), Severity.Info);
    }

    void AddWarningSnack(string message)
    {
        Snackbar.Add(FormatMessage(message), Severity.Warning);
    }

    void AddFailureSnack(string message)
    {
        Snackbar.Add(FormatMessage(message), Severity.Error, config => config.SetVisibleDuration(_failedDuration));
    }

    void AddSuccessSnack(string message)
    {
        Snackbar.Add(FormatMessage(message), Severity.Success);
    }

    MarkupString FormatMessage(string message)
    {
        if (!message.Contains(Environment.NewLine))
        {
            return new MarkupString(message);
        }

        var listBuilder = new StringBuilder();
        listBuilder.Append("<ul>");
        foreach (var line in message.Split(Environment.NewLine))
        {
            listBuilder.Append($"<li>{line}</li>");
        }
        listBuilder.Append("</ul>");
        return new MarkupString(listBuilder.ToString());
    }
}
