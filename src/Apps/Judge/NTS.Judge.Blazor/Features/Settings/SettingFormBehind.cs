using System.Globalization;
using Not.Application.Services;
using Not.Blazor.Components.Input;
using Not.Blazor.CRUD.Forms.Components;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Settings;

namespace NTS.Judge.Blazor.Features.Settings;

public class SettingFormBehind : NForm<SettingFormModel>
{
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ISeeker<Country> CountrySeeker { get; set; } = default!;

    [Inject]
    NavigationManager NavManager { get; set; } = default!;

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

    protected async Task<IEnumerable<Country?>> SearchLocalizedCountry(string term, CancellationToken ct)
    {
        var foundCountries = await CountrySeeker.Search(term, ct);
        return foundCountries.Where(x => x.Locale != null);
    }

    protected async Task<IEnumerable<Country?>> SearchCountrySafe(string term, CancellationToken ct)
    {
        return await CountrySeeker.Search(term, ct);
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Setting.Country), () => _countryField);
    }
}
