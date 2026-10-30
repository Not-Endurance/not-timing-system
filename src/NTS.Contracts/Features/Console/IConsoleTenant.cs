using Not.Application.Behinds.Adapters;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The Tenant the person acts in, as the Console's page of it shows it (#646, ADR-0012): its name, the rules of its Regional
/// competitions, and what the person may do about them, which is what the Api says (<c>GET /api/tenants/{id}/capabilities</c>)
/// and not a rule of the Ui. The rules an Event copies when it starts are the Tenant's at that moment, so an edit here changes
/// the Events that start after it and no Event that has. It follows the account.
/// </summary>
public interface IConsoleTenant : IStatefulService
{
    /// <summary>The Tenant the person acts in: none for a person who has none.</summary>
    string? TenantId { get; }

    string Name { get; }

    /// <summary>Whether the person may edit the rules of the Tenant: a Tenant Root of it.</summary>
    bool CanEditRules { get; }

    /// <summary>Whether the person may edit the branding of the Tenant: a Tenant Root of it.</summary>
    bool CanEditBranding { get; }

    /// <summary>Whether a Regional competition judges the speed of a loop on the average only.</summary>
    bool OnlyAverageLoopSpeed { get; }

    /// <summary>The code of the regional ranker, none for the ranking of the FEI.</summary>
    string? RankerCode { get; }

    /// <summary>Saves the rules of the Tenant. What the Api does not take is thrown as a domain error, in its words.</summary>
    Task SaveRules(bool onlyAverageLoopSpeed, string? rankerCode);
}
