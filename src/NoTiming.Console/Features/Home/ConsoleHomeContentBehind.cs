using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using Not.Notify;
using NoTiming.Console.Features.Home.Dialogs;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;
using NTS.Domain.Setup.Services.StartValidation;

namespace NoTiming.Console.Features.Home;

/// <summary>
/// The home of the Console (#646): the Events the person runs or, for a Tenant Root, the Tenant's, each at its stage with
/// the actions the Api says the person may take (<see cref="ConsoleEvent"/>), and the making of an Event.
/// </summary>
public class ConsoleHomeContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected IReadOnlyList<ConsoleEvent> Rows => Events.Events;
    protected bool CanCreate => Events.CanCreate;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }

    protected static string StageText(EventStage stage)
    {
        return stage switch
        {
            EventStage.Live => Event_stage_live_string,
            EventStage.Historic => Event_stage_historic_string,
            _ => Event_stage_unstarted_string,
        };
    }

    protected static Color ColorOf(EventStage stage)
    {
        return stage switch
        {
            EventStage.Live => Color.Success,
            EventStage.Historic => Color.Default,
            _ => Color.Info,
        };
    }

    protected static bool ShowSetup(ConsoleEvent row)
    {
        return row is { Stage: EventStage.Unstarted, IsMainOperator: true };
    }

    protected static bool ShowStart(ConsoleEvent row)
    {
        return row is { Stage: EventStage.Unstarted, CanConfigure: true, IsMainOperator: true };
    }

    protected static bool ShowDashboard(ConsoleEvent row)
    {
        return row is { Stage: EventStage.Live, IsMainOperator: true };
    }

    /// <summary>The Results of an Event that is over, or of a Live one that the person does not run: the Console's page of them prints and exports.</summary>
    protected static bool ShowResults(ConsoleEvent row)
    {
        return row.Stage == EventStage.Historic || row is { Stage: EventStage.Live, IsMainOperator: false };
    }

    protected void OpenHistoricEvents()
    {
        Navigator.NavigateTo(Routes.HISTORIC_EVENTS_PAGE);
    }

    protected void OpenAthletesAndHorses()
    {
        Navigator.NavigateTo(ConsoleRoutes.ATHLETES_HORSES_PAGE);
    }

    protected void OpenClubs()
    {
        Navigator.NavigateTo(ConsoleRoutes.CLUBS_PAGE);
    }

    protected void OpenResults(ConsoleEvent row)
    {
        Navigator.NavigateTo(Routes.Of(ConsoleRoutes.EVENT_RANKINGS_PAGE, row.Id));
    }

    protected void OpenSetup(ConsoleEvent row)
    {
        Navigator.NavigateTo(Routes.Of(ConsoleRoutes.EVENT_SETUP_PAGE, row.Id));
    }

    protected void OpenDashboard(ConsoleEvent row)
    {
        Navigator.NavigateTo(Routes.Of(ConsoleRoutes.EVENT_DASHBOARD_PAGE, row.Id));
    }

    protected async Task Create()
    {
        await Safely(async () =>
        {
            var dialog = await DialogService.ShowAsync<CreateEventDialog>(Create_event_string);
            var result = await dialog.Result;
            if (result is not { Canceled: false, Data: NewEvent made })
            {
                return;
            }

            await Events.Create(made.Name, made.Location, made.FeiShowId);
        });
    }

    protected async Task Start(ConsoleEvent row)
    {
        await Safely(async () =>
        {
            var issues = await Events.Validate(row.Id);
            if (issues.Count > 0 && !await Settle(row, issues))
            {
                return;
            }

            await Events.Start(row.Id);
            Notifier.Success(Event_started_string);
            OpenDashboard(row);
        });
    }

    protected async Task Reset(ConsoleEvent row)
    {
        await Safely(async () =>
        {
            var dialog = await DialogService.ShowAsync<ResetEventDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }

            await Events.Reset(row.Id);
            Notifier.Success(Event_was_reset_string);
        });
    }

    protected async Task Assign(ConsoleEvent row)
    {
        await ChooseMainOperator(
            Assign_main_operator_string,
            async account =>
            {
                await Events.AssignMainOperator(row.Id, account.Id);
                Notifier.Success(Main_operator_assigned_string);
            }
        );
    }

    protected async Task HandOver(ConsoleEvent row)
    {
        await ChooseMainOperator(
            Hand_over_event_string,
            async account =>
            {
                await Events.HandOver(row.Id, account.Id);
                Notifier.Success(Event_handed_over_string);
            }
        );
    }

    async Task ChooseMainOperator(string title, Func<AccountMatch, Task> act)
    {
        await Safely(async () =>
        {
            var dialog = await DialogService.ShowAsync<MainOperatorDialog>(title);
            var result = await dialog.Result;
            if (result is { Canceled: false, Data: AccountMatch account })
            {
                await act(account);
            }
        });
    }

    /// <summary>Says what is missing, and whether the person settled it there: a start number that two Competitions hold at once is corrected in the dialog.</summary>
    async Task<bool> Settle(ConsoleEvent row, IReadOnlyList<StartValidationIssue> issues)
    {
        var parameters = new DialogParameters<StartIssuesDialog>
        {
            { x => x.Issues, issues },
            { x => x.EventId, row.Id },
        };
        var dialog = await DialogService.ShowAsync<StartIssuesDialog>(Invalid_setup_string, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: true };
    }

    /// <summary>What the Api refuses is said, in its own words, to the person; anything else is an error of the app.</summary>
    async Task Safely(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
