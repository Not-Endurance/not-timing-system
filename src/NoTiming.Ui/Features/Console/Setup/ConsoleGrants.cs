using Not.Domain.Exceptions;
using Not.Injection;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Access.Models;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>
/// The grants of an Event (#646, ADR-0012): the people the Console Operator linked to its Officials and Operators, by the exact
/// email. The Api decides who may, and says why when it does not (an Event that has ended, an address it cannot read, the
/// limit of grants an Event has): what it says is thrown as a domain error, so that the form that asked shows it and keeps
/// what was typed.
/// </summary>
public class ConsoleGrants : IConsoleGrants, ITransient
{
    readonly EventGrantApiRepository _grants;

    public ConsoleGrants(EventGrantApiRepository grants)
    {
        _grants = grants;
    }

    public async Task<IReadOnlyList<GrantRow>> List(Guid eventId)
    {
        var grants = await ApiRefusals.Answered(() => _grants.ListOf(eventId));
        return [.. grants.Select(RowOf)];
    }

    public async Task<GrantRow> Link(Guid eventId, GrantKind kind, OfficialRole? role, string email)
    {
        var grant = new EventGrantModel
        {
            EventId = eventId,
            Kind = kind,
            OfficialRole =
                kind == GrantKind.Official ? role ?? throw new DomainException(Select_a_role_string) : null,
            Email = email.Trim(),
        };
        return RowOf(await ApiRefusals.Answered(() => _grants.Link(grant)));
    }

    public Task Remove(Guid grantId)
    {
        return ApiRefusals.Answered(() => _grants.Delete(grantId));
    }

    static GrantRow RowOf(EventGrantModel grant)
    {
        return new GrantRow
        {
            Id = grant.Id,
            Kind = grant.Kind,
            Role = grant.OfficialRole,
            IsPending = grant.IsPending,
            DisplayName = grant.DisplayName,
            Email = grant.Email ?? string.Empty,
        };
    }
}
