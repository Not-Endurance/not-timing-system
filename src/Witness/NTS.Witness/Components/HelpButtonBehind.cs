using Not.Blazor.Components;

namespace NTS.Witness.Components;

public class HelpButtonBehind : NComponent
{
    [Parameter]
    public Action ClickHandler { get; set; } = default!;

    public bool IsEmergencyContactsConfigured { get; set; } = true;
}
