using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Core.Events;
using NoTiming.Ui.Features.Participants;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using static NoTiming.Ui.Features.Console.JsonAttributes;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Events of the Console's home (#646). What the person sees is what the Api lists for them, and what they may do about
/// each is what the Api says for the person and the Event as they are now (<c>GET /api/events/{id}/capabilities</c>): the
/// Ui keeps no rule of its own about who may configure, start, reset or assign. It follows the account.
/// </summary>
public class ConsoleEvents : AccountAwareContext, IConsoleEvents, IScoped
{
    readonly ConfigureEventApiRepository _setups;
    readonly IEventInformationRepository _events;
    readonly ConsoleOperatorApiRepository _operators;
    readonly AccountApiRepository _accounts;
    readonly TenantApiRepository _tenants;
    IReadOnlyList<ConsoleEvent> _list = [];

    public ConsoleEvents(
        IAccountSession account,
        ConfigureEventApiRepository setups,
        IEventInformationRepository events,
        ConsoleOperatorApiRepository operators,
        AccountApiRepository accounts,
        TenantApiRepository tenants
    )
        : base(account)
    {
        _setups = setups;
        _events = events;
        _operators = operators;
        _accounts = accounts;
        _tenants = tenants;
    }

    public IReadOnlyList<ConsoleEvent> Events => _list;

    public bool CanCreate { get; private set; }

    public bool CanEditRegistry { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        await Account.Load();
        if (!Account.IsKnown)
        {
            return false; // the host has not said who is signed in: ask again
        }

        if (Account.Current is not { } person)
        {
            _list = [];
            CanCreate = false;
            CanEditRegistry = false;
            return true;
        }

        var setups = (await _setups.ReadMany()).ToList();
        var rows = await Task.WhenAll(setups.Select(RowOf));
        var tenant = await TenantMay(person.CurrentTenantId);
        _list = [.. rows.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        CanCreate = tenant.CanCreateEvents;
        CanEditRegistry = tenant.CanEditRegistry;
        return true;
    }

    public Task Refresh()
    {
        return ReloadState();
    }

    public async Task<ConsoleEvent> Create(string name, string location, string? feiShowId)
    {
        var id = await ApiRefusals.Answered(() =>
            _setups.Make(
                new ConfigureEventDraft
                {
                    Name = name,
                    Location = location,
                    FeiShowId = feiShowId,
                }
            )
        );
        await ReloadState();
        return _list.Single(x => x.Id == id);
    }

    public async Task<IReadOnlyList<StartValidationIssue>> Validate(Guid setupId)
    {
        var setup = await _setups.Read(setupId) ?? throw new DomainException(Event_not_found_string);
        return StartValidator.Validate(setup).Data ?? [];
    }

    public async Task Start(Guid setupId)
    {
        await ApiRefusals.Answered(() => _events.Start(setupId));
        await ReloadState();
    }

    public async Task Reset(Guid eventId)
    {
        await ApiRefusals.Answered(() => _events.Remove(eventId));
        await ReloadState();
    }

    public async Task<IReadOnlyList<ConsoleOperatorRow>> ConsoleOperators(Guid eventId)
    {
        var operators = await ApiRefusals.Answered(() => _operators.ListOf(eventId));
        return
        [
            .. operators.Select(x => new ConsoleOperatorRow(
                x.Id,
                x.DisplayName ?? string.Empty,
                x.Email ?? string.Empty,
                x.Pending
            )),
        ];
    }

    public async Task AssignConsoleOperator(Guid eventId, Guid accountId)
    {
        await ApiRefusals.Answered(() => _operators.Assign(eventId, accountId));
    }

    public async Task RemoveConsoleOperator(Guid consoleOperatorId)
    {
        await ApiRefusals.Answered(() => _operators.Delete(consoleOperatorId));
    }

    public async Task<IReadOnlyList<AccountMatch>> FindAccounts(string text)
    {
        if (!ParticipantService.IsSearchable(text))
        {
            return []; // the Api refuses a text with no word of two characters, and a person who is still typing is not told off
        }

        var words = string.Join(' ', ParticipantService.WordsOf(text));
        var found = await ApiRefusals.Answered(() =>
            _accounts.Search($"contains(name,'{words.Replace("'", "''")}')")
        );
        return
        [
            .. found.Select(x => new AccountMatch(
                Guid.Parse(x.GetProperty("id").GetString()!),
                Text(x.GetProperty("attributes"), "displayName") ?? string.Empty,
                Text(x.GetProperty("attributes"), "email") ?? string.Empty
            )),
        ];
    }

    async Task<ConsoleEvent> RowOf(ConfigureEvent setup)
    {
        var may = await ApiRefusals.Answered(() => _events.CapabilitiesOf(setup.Id));
        return new ConsoleEvent
        {
            Id = setup.Id,
            Name = setup.Name,
            Location = setup.Location,
            Stage = may.Stage,
            Level = may.Level,
            CanConfigure = may.CanConfigure,
            CanEditSetup = may.CanEditSetup,
            CanRun = may.CanRun,
            CanAssignConsoleOperators = may.CanAssignConsoleOperators,
        };
    }

    /// <summary>What the Api says the person may do in the Tenant they act in: make Events there, and write to its registries.</summary>
    async Task<(bool CanCreateEvents, bool CanEditRegistry)> TenantMay(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return (false, false);
        }

        try
        {
            var may = await _tenants.CapabilitiesOf(tenantId);
            return (may.CanCreateEvents, may.CanEditRegistry);
        }
        catch (JsonApiException)
        {
            return (false, false); // what the Api does not say, the person may not do
        }
    }
}
