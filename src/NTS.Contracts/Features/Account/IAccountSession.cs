using Not.Application.Behinds.Adapters;

namespace NTS.Contracts.Features.Account;

/// <summary>
/// Who the Ui is looking at (ADR-0002, #645). The host knows: the session is a cookie the browser keeps and the Ui cannot
/// read, so the Ui asks, and shows what the answer allows. There is no token, no sign-in state of its own and nothing in
/// the browser's storage. Signing in is a page of the host that returns to the app, and signing out is the host ending the
/// session.
/// </summary>
public interface IAccountSession : IStatefulService
{
    /// <summary>The account the session cookie belongs to, none for a visitor and until the host has been asked.</summary>
    CurrentAccount? Current { get; }

    bool IsSignedIn { get; }

    /// <summary>Whether the host has said who is signed in, nobody included. A host that could not be reached has said nothing.</summary>
    bool IsKnown { get; }

    /// <summary>Asks the host again who is signed in, and tells who watches.</summary>
    Task Refresh();

    /// <summary>Ends the session at the host, forgets the account and tells who watches.</summary>
    Task SignOut();

    /// <summary>
    /// Selects the Tenant that what the person reads and writes outside an Event belongs to, none to go back to the home
    /// Tenant. It has to be one the person is a member of.
    /// </summary>
    Task SelectTenant(string? tenantId);
}
