using System.Globalization;
using Not.Application.Services;
using Not.Blazor.Components.Input;
using Not.Blazor.CRUD.Forms.Components;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.Services;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings.Components;

public partial class SettingForm : NForm<SettingFormModel>
{
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ILanguageSeeker LanguageSeeker { get; set; } = default!;

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

    protected async Task<IEnumerable<Country?>> SearchCountrySafe(string term, CancellationToken ct)
    {
        return await CountrySeeker.Search(term, ct);
    }

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Setting.Country), () => _countryField);
    }
}
