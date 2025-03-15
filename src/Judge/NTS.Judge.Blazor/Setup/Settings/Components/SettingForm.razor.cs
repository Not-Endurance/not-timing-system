using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Components;
using NTS.Domain.Aggregates;
using NTS.Domain.Settings;

namespace NTS.Judge.Blazor.Setup.Settings.Components;

public partial class SettingForm : NForm<SettingFormModel>
{
    NAutocomplete<Country?> _countryField = default!;
    NSelect<DetectionMode?> _detectionModeField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(Setting.Country), () => _countryField);
        RegisterInjector(nameof(Setting.DetectionMode), () => _detectionModeField);
    }
}
