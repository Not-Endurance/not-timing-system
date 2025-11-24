using Not.Blazor.Components;

namespace NTS.Witness.Blazor.Components;

public class HelpButtonBehind : NComponent
{
    [Parameter]
    public Action ClickHandler { get; set; } = default!;

    public bool IsEmergencyContactsConfigured { get; set; } = true;
}
