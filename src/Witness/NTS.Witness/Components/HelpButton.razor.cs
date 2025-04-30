using Microsoft.AspNetCore.Components;
using Not.Blazor.Navigation;
using NTS.Witness.Constants;

namespace NTS.Witness.Components;

public partial class HelpButton
{
    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;
    public bool IsEmergencyContactsConfigured { get; set; } = true;
    public bool IsCurrentPageEmergencyContacts => Navigator.CurrentEndpoint == Endpoints.EMERGENCY_CONTACTS;

    void HelpHandler()
    {
        Navigator.NavigateTo(Endpoints.EMERGENCY_CONTACTS);
    }
}

