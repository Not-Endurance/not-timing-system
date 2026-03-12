using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor.Layout;

public class HelpButtonBehind : NComponent
{
    [Parameter]
    public Action ClickHandler { get; set; } = default!;

    public bool IsEmergencyContactsConfigured { get; set; } = true;
}
