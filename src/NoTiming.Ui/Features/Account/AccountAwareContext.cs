using Not.Application.Behinds.Adapters;
using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

/// <summary>
/// State that depends on who is signed in. The account is learned after the app has booted, when the host answers, and it
/// changes when a person signs out, so a context that was loaded for a visitor has to load again when the account changes,
/// or the drawer would show a visitor's state to a signed-in person until the next full page load.
/// </summary>
public abstract class AccountAwareContext : NStatefulService
{
    protected AccountAwareContext(IAccountSession account)
    {
        Account = account;
        Observe(account, () => _ = ReloadState());
    }

    protected IAccountSession Account { get; }
}
