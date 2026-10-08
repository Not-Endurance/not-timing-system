using Not.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

/// <summary>
/// What the person keeps about the app on the account page (#645): the language, which is a choice of this browser, the
/// Tenant that what they read and write belongs to, which is shown only to a person who holds a Membership in more than one,
/// and the way to the passkeys, which are a page of the host.
/// </summary>
public class AccountSettingsBehind : NStatefulComponent
{
    [Inject]
    IAccountSession Account { get; set; } = default!;

    [Inject]
    ILanguagePreference Language { get; set; } = default!;

    [Inject]
    ITenantDirectory Tenants { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected IReadOnlyList<TenantOption> TenantOptions { get; private set; } = [];

    protected IEnumerable<NotListModel<string>> LanguageItems =>
        Languages.Supported.Select(x => new NotListModel<string>(x.Code, x.Name));

    protected IEnumerable<NotListModel<string>> TenantItems =>
        TenantOptions.Select(x => new NotListModel<string>(x.Id, x.Name));

    protected string? Email => Account.Current?.Email;
    protected string CurrentLanguage => Language.Current;
    protected string? CurrentTenant => Account.Current?.CurrentTenantId;
    protected bool ShowTenants => TenantOptions.Count > 1;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Account);
        try
        {
            TenantOptions = await Tenants.OptionsOfTheAccount();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>The choice is kept in the browser and the app is loaded again, as every text is written in the language when it is rendered.</summary>
    protected async Task ChooseLanguage(string code)
    {
        try
        {
            if (code == CurrentLanguage)
            {
                return;
            }

            await Language.Choose(code);
            Navigator.NavigateTo(Navigator.Uri, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ChooseTenant(string? tenantId)
    {
        try
        {
            if (tenantId != CurrentTenant)
            {
                await Account.SelectTenant(tenantId);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>The passkeys are managed on a page of the host (ADR-0002), which the app leaves for.</summary>
    protected void OpenPasskeys()
    {
        Navigator.NavigateTo(Routes.PASSKEYS_PAGE, forceLoad: true);
    }
}
