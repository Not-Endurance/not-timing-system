using NTS.Witness.Platforms.Services;

namespace NTS.Witness.Components.Pages;

public partial class EmergencyContacts
{
    [Inject]
    IDialService DialService { get; set; } = default!;

    public Dictionary<string, string> Contacts { get; set; } = default!;

    protected override void OnInitialized()
    {
        Contacts = new Dictionary<string, string>
        {
            { "Yo mama", "+359 882312321" },
            { "Baba yaga", "+359 666666666" },
        };
    }

    public void OnNumberClick(string phoneNumber)
    {
        DialService.DialNumber(phoneNumber);
    }
}
