using Not.Blazor.Components;
using NTS.Witness.Platforms.Services;

namespace NTS.Witness.Components.Pages;

public class EmergencyContactsBehind : NComponent
{
    [Inject]
    IDialService DialService { get; set; } = default!;

    protected Dictionary<string, string> Contacts { get; set; } = default!;

    protected override void OnInitialized()
    {
        Contacts = new Dictionary<string, string>
        {
            { "Yo mama", "+359 882312321" },
            { "Baba yaga", "+359 666666666" },
        };
    }

    protected void OnNumberClick(string phoneNumber)
    {
        DialService.DialNumber(phoneNumber);
    }
}
