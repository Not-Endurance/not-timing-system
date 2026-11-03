using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Not.Serialization.JSON;

public class PrivatePropertySetterResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (!property.Writable && member is PropertyInfo propertyInfo && propertyInfo.GetSetMethod(true) != null)
        {
            property.Writable = true;
        }

        return property;
    }
}
