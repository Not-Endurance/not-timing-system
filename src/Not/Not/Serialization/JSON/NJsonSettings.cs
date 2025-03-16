using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Not.Serialization.JSON;

public class NJsonSettings : JsonSerializerSettings
{
    public NJsonSettings()
    {
        ContractResolver = new PrivatePropertySetterResolver();
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
        Formatting = Newtonsoft.Json.Formatting.Indented;
        TypeNameHandling = TypeNameHandling.Auto;
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
        Converters = [new StringEnumConverter()];
    }
}
