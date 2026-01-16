using Not.Blazor.Components;
using NTS.Witness.Services;

namespace NTS.Witness.Blazor.Components.EmergencyContacts;

public class EmergencyContactsBehind : NComponent
{
    [Inject]
    IEmergencyService Service { get; set; } = default!;

    protected Dictionary<string, string> Contacts => Service.Contacts;
}
