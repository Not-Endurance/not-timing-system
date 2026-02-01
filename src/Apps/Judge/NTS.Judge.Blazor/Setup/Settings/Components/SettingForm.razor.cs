using System.Globalization;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Components;
using Not.Krud.Blazor.Components;
using NTS.Domain.Aggregates;
using NTS.Domain.Settings;
using NTS.Judge.Features.Setup.Services;
using NTS.Judge.Features.Setup.Settings;

namespace NTS.Judge.Blazor.Setup.Settings.Components;

public partial class SettingForm : NForm<SettingFormModel>
{
    NAutocomplete<Country?> _countryField = default!;

    [Inject]
    ILanguageSeeker LanguageSeeker { get; set; } = default!;

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

    // NSelect<DetectionMode?> _detectionModeField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Setting.Country), () => _countryField);
        // RegisterInjector(nameof(Setting.DetectionMode), () => _detectionModeField);
    }
}
