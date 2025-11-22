using Not.Blazor.Components;

namespace NTS.Witness.Web.Components.Pages;

public class EmergencyContactsBehind : NComponent
{
    protected Dictionary<string, string> Contacts { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            Contacts = DummyData.CreateContacts();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
