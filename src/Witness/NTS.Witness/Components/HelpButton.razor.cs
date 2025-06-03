using Microsoft.AspNetCore.Components;
using Not.Blazor.Navigation;
using NTS.Witness.Constants;

namespace NTS.Witness.Components;

public partial class HelpButton
{
    [Parameter]
    public Action ClickHandler { get; set; } = default!;

    public bool IsEmergencyContactsConfigured { get; set; } = true;
}
