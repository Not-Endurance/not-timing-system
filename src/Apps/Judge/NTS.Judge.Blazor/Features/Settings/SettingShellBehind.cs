using System.Globalization;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Contracts.Features.Settings;

namespace NTS.Judge.Blazor.Features.Settings;

public class SettingShellBehind : KrudShell<SettingFormModel>
{
    [Inject]
    IJudgeSetupLookupService Lookups { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    protected Task SetUiCulture()
    {
        try
        {
            if (Model.LanguageCountry == null)
            {
                return Task.CompletedTask;
            }

            var culture = new CultureInfo(Model.LanguageCountry.Locale!);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            NavManager.NavigateTo(NavManager.Uri, forceLoad: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        return Task.CompletedTask;
    }

    protected async Task<IEnumerable<Country?>> SearchLanguageSafe(string term, CancellationToken ct)
    {
        var foundCountries = await Lookups.SearchCountries(term, ct);
        return foundCountries.Where(x => x.Locale != null);
    }

    protected async Task<IEnumerable<Country?>> SearchCountrySafe(string term, CancellationToken ct)
    {
        return await Lookups.SearchCountries(term, ct);
    }
}
