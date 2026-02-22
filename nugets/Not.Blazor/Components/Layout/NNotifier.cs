using System.Text;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Events;
using Not.Notify;

namespace Not.Blazor.Components.Layout;

public class NNotifier : NComponent, IDisposable
{
    readonly List<(Guid Id, IEventSubscriber<string> Subscriber)> _subscriptions = [];
    readonly TimeSpan _failedDuration = TimeSpan.FromSeconds(30);

    [Inject]
    ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    INotificationStream NotificationStream { get; set; } = default!;

    protected override void OnInitialized()
    {
        Subscribe(NotificationStream.Informed, AddInformationSnack);
        Subscribe(NotificationStream.Succeeded, AddSuccessSnack);
        Subscribe(NotificationStream.Warned, AddWarningSnack);
        Subscribe(NotificationStream.Failed, AddFailureSnack);
    }

    public void Dispose()
    {
        foreach (var (id, subscriber) in _subscriptions)
        {
            subscriber.Unsubscribe(id);
        }
        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }

    void Subscribe(IEventSubscriber<string> subscriber, Action<string> callback)
    {
        var id = subscriber.SubscribeAsync(callback);
        _subscriptions.Add((id, subscriber));
    }

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
