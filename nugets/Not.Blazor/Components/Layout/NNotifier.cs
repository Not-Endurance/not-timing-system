using System.Text;
using MudBlazor;
using Not.Application.Environments;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs;
using Not.Blazor.Helpers;
using Not.Events;
using Not.Notify;

namespace Not.Blazor.Components.Layout;

public class NNotifier : NComponent, IDisposable
{
    readonly List<(Guid Id, IEventSubscriber<string> Subscriber)> _stringSubscriptions = [];
    readonly List<(Guid Id, IEventSubscriber<Exception> Subscriber)> _exceptionSubscriptions = [];
    readonly TimeSpan _failedDuration = TimeSpan.FromSeconds(30);
    readonly DialogOptions _nonProductionExceptionDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraLarge,
    };
    readonly DialogOptions _productionExceptionDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        FullWidth = false,
        MaxWidth = MaxWidth.Small,
    };

    [Inject]
    IEnvironmentContext EnvironmentContext { get; set; } = default!;

    [Inject]
    ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INotificationStream NotificationStream { get; set; } = default!;

    protected override void OnInitialized()
    {
        Subscribe(NotificationStream.Informed, AddInformationSnack);
        Subscribe(NotificationStream.Succeeded, AddSuccessSnack);
        Subscribe(NotificationStream.Warned, AddWarningSnack);
        Subscribe(NotificationStream.Failed, AddFailureSnack);
        Subscribe(NotificationStream.UnhandledExceptions, ShowExceptionHandlerDialog);
    }

    public override void Dispose()
    {
        foreach (var (id, subscriber) in _stringSubscriptions)
        {
            subscriber.Unsubscribe(id);
        }
        _stringSubscriptions.Clear();

        foreach (var (id, subscriber) in _exceptionSubscriptions)
        {
            subscriber.Unsubscribe(id);
        }
        _exceptionSubscriptions.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    void Subscribe(IEventSubscriber<string> subscriber, Action<string> callback)
    {
        var id = subscriber.SubscribeAsync(callback);
        _stringSubscriptions.Add((id, subscriber));
    }

    void Subscribe(IEventSubscriber<Exception> subscriber, Func<Exception, Task> callback)
    {
        var id = subscriber.SubscribeAsync(callback);
        _exceptionSubscriptions.Add((id, subscriber));
    }

    void AddInformationSnack(string message)
    {
        var formattedMessage = FormatMessage(message);
        Snackbar.Add(formattedMessage, Severity.Info);
    }

    void AddWarningSnack(string message)
    {
        var formattedMessage = FormatMessage(message);
        Snackbar.Add(formattedMessage, Severity.Warning);
    }

    void AddFailureSnack(string message)
    {
        var formattedMessage = FormatMessage(message);
        Snackbar.Add(formattedMessage, Severity.Error, config => config.SetVisibleDuration(_failedDuration));
    }

    void AddSuccessSnack(string message)
    {
        var formattedMessage = FormatMessage(message);
        Snackbar.Add(formattedMessage, Severity.Success);
    }

    async Task ShowExceptionHandlerDialog(Exception exception)
    {
        try
        {
            await InvokeAsync(async () =>
            {
                var parameters = new DialogParameters<NExceptionHandlerDialog> { { x => x.Exception, exception } };
                var dialogOptions = EnvironmentContext.IsProduction()
                    ? _productionExceptionDialogOptions
                    : _nonProductionExceptionDialogOptions;

                await DialogService.ShowAsync<NExceptionHandlerDialog>(string.Empty, parameters, dialogOptions);
            });
        }
        catch (Exception dialogException)
        {
            Console.Error.WriteLine(dialogException);
        }
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
