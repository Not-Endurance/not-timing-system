using Not.Blazor.Components;
using Not.Safe;
using NTS.Witness.Platforms.Services;

namespace NTS.Witness.Components.Pages;

public class EmergencyContactsBehind : NComponent
{
    [Inject]
    IDialService DialService { get; set; } = default!;

    protected Dictionary<string, string> Contacts { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            Contacts = DummyData.CreateContacts();
        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }

    protected void OnNumberClick(string phoneNumber)
    {
        try
        {
            DialService.DialNumber(phoneNumber);
        }
        catch (Exception ex)
        {
            SafeHelper.HandleError(ex);
        }
    }

}
