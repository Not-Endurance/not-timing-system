using Microsoft.Extensions.Configuration;
using Not.Injection;

namespace NTS.Witness.Services;

public class EmergencyContext : IEmergencyContext, ISingleton
{
    const string SECTION_NAME = "EmergencyContacts";

    public EmergencyContext(IConfiguration configuration)
    {
        var contacts = configuration.GetSection(SECTION_NAME).Get<Dictionary<string, string>>();
        Contacts = contacts?.Count > 0 ? contacts : DummyData.CreateContacts();
    }

    public Dictionary<string, string> Contacts { get; }
}
