using Not.Blazor.Components.Abstractions;
using NTS.Witness.Features.Support.EmergencyContacts;

namespace NTS.Witness.Blazor.Features.Support.EmergencyContacts;

public class EmergencyContactsContentBehind : NComponent
{
    [Inject]
    IEmergencyContactsService EmergencyContactsService { get; set; } = default!;

    protected Dictionary<string, string> Contacts => EmergencyContactsService.Contacts;
}
