using System.Globalization;
using Not.Application.Services;
using Not.Krud.Blazor.Components.Abstractions;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Settings;

namespace NTS.Judge.Blazor.Features.Settings;

public class SettingShellBehind : KrudShell<SettingFormModel>
{
    [Inject]
    ISeeker<Country> CountrySeeker { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

    protected override void OnInitialized()
    {
        ;
    }

    protected Task SetUiCulture()
    {
        if (Model.LanguageCountry == null)
        {
            return Task.CompletedTask;
        }

        var culture = new CultureInfo(Model.LanguageCountry.Locale!);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        NavManager.NavigateTo(NavManager.Uri, forceLoad: true);
        return Task.CompletedTask;
    }

    protected async Task<IEnumerable<Country?>> SearchLanguageSafe(string term, CancellationToken ct)
    {
        var foundCountries = await CountrySeeker.Search(term, ct);
        return foundCountries.Where(x => x.Locale != null);
    }

    protected async Task<IEnumerable<Country?>> SearchCountrySafe(string term, CancellationToken ct)
    {
        return await CountrySeeker.Search(term, ct);
    }
}
