using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Not.Serialization.JSON.Converters;

namespace Not.Serialization.JSON;

public class NJsonSettings : JsonSerializerSettings
{
    public static NJsonSettings ConfigureServerSerialization()
    {
        var settings = new NJsonSettings();
        //settings.ConfigureServer();
        return settings;
    }

    public NJsonSettings()
    {
        ContractResolver = new PrivatePropertySetterResolver();
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
        Formatting = Newtonsoft.Json.Formatting.Indented;
        TypeNameHandling = TypeNameHandling.Auto;
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
        Converters = [new StringEnumConverter()];
    }

    public void ConfigureServer()
    {
        Converters.Add(new DateTimeOffsetUtcSerializer());
        DateTimeZoneHandling = DateTimeZoneHandling.Utc;
    }
}
