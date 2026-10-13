using System.Text.Json;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NTS.Application.Core;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using static NoTiming.Ui.Features.Console.ApiAnswers;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Events of the Console's home (#646). What the person sees is what the Api lists for them, and what they may do about
/// each is what the Api says for the person and the Event as they are now (<c>GET /api/events/{id}/capabilities</c>): the
/// Ui keeps no rule of its own about who may configure, start, reset or hand over. It follows the account.
/// </summary>
public class ConsoleEvents : AccountAwareContext, IConsoleEvents, IScoped
{
    readonly IRepository<ConfigureEvent> _setups;
    readonly IEventInformationRepository _events;
    readonly JsonApiClient _api;
    IReadOnlyList<ConsoleEvent> _list = [];

    public ConsoleEvents(
        IAccountSession account,
        IRepository<ConfigureEvent> setups,
        IEventInformationRepository events,
        JsonApiClient api
    )
        : base(account)
    {
        _setups = setups;
        _events = events;
        _api = api;
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
        var response = await _api.Send(
            HttpMethod.Post,
            "configure-events",
            Document(
                "configure-events",
                null,
                new
                {
                    name,
                    location,
                    feiShowId,
                }
            )
        );
        var made = Accepted(response).Document!.Value.GetProperty("data");
        var id = Guid.Parse(made.GetProperty("id").GetString()!);
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
        await _events.Start(setupId);
        await ReloadState();
    }

    public async Task Reset(Guid eventId)
    {
        Accepted(await _api.Send(HttpMethod.Delete, $"events/{eventId}"));
        await ReloadState();
    }

    public async Task AssignMainOperator(Guid eventId, Guid accountId)
    {
        Accepted(
            await _api.Send(
                HttpMethod.Post,
                $"events/{eventId}/actions/assign-main-operator",
                Document("main-operators", null, new { accountId })
            )
        );
        await ReloadState();
    }

    public async Task HandOver(Guid eventId, Guid accountId)
    {
        Accepted(
            await _api.Send(
                HttpMethod.Post,
                $"events/{eventId}/actions/hand-over",
                Document("main-operators", null, new { accountId })
            )
        );
        await ReloadState();
    }

    public async Task<IReadOnlyList<AccountMatch>> FindAccounts(string text)
    {
        var filter = Uri.EscapeDataString($"contains(name,'{text.Replace("'", "''")}')");
        var found = Accepted(await _api.Send(HttpMethod.Get, $"accounts?filter={filter}"));
        return
        [
            .. found
                .Document!.Value.GetProperty("data")
                .EnumerateArray()
                .Select(x => new AccountMatch(
                    Guid.Parse(x.GetProperty("id").GetString()!),
                    Text(x.GetProperty("attributes"), "displayName") ?? string.Empty,
                    Text(x.GetProperty("attributes"), "email") ?? string.Empty
                )),
        ];
    }

    async Task<ConsoleEvent> RowOf(ConfigureEvent setup)
    {
        var capabilities = Accepted(await _api.Send(HttpMethod.Get, $"events/{setup.Id}/capabilities"))
            .Document!.Value.GetProperty("data")
            .GetProperty("attributes");
        return new ConsoleEvent
        {
            Id = setup.Id,
            Name = setup.Name,
            Location = setup.Location,
            Stage = Enum.Parse<EventStage>(capabilities.GetProperty("stage").GetString()!, ignoreCase: true),
            IsMainOperator = Flag(capabilities, "isMainOperator"),
            CanConfigure = Flag(capabilities, "canConfigure"),
            CanEditSetup = Flag(capabilities, "canEditSetup"),
            CanEditData = Flag(capabilities, "canEditData"),
            CanLinkAccounts = Flag(capabilities, "canLinkAccounts"),
            CanEditBranding = Flag(capabilities, "canEditBranding"),
            CanReset = Flag(capabilities, "canReset"),
            CanHandOver = Flag(capabilities, "canHandOver"),
            CanAssignMainOperator = Flag(capabilities, "canAssignMainOperator"),
        };
    }

    /// <summary>What the Api says the person may do in the Tenant they act in: make Events there, and write to its registries.</summary>
    async Task<(bool CanCreateEvents, bool CanEditRegistry)> TenantMay(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return (false, false);
        }

        var response = await _api.Send(HttpMethod.Get, $"tenants/{Uri.EscapeDataString(tenantId)}/capabilities");
        if (response is not { IsSuccess: true, Document: { } document })
        {
            return (false, false);
        }

        var may = document.GetProperty("data").GetProperty("attributes");
        return (Flag(may, "canCreateEvents"), Flag(may, "canEditRegistry"));
    }

    static JsonElement Document(string type, string? id, object attributes)
    {
        return JsonSerializer.SerializeToElement(
            new
            {
                data = new
                {
                    type,
                    id,
                    attributes,
                },
            },
            JsonApiClient.Options
        );
    }
}
