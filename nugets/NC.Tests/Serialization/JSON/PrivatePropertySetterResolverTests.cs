using Newtonsoft.Json;
using NC.Serialization.JSON;
using Xunit;

namespace NC.Tests.Serialization.JSON;

public sealed class PrivatePropertySetterResolverTests
{
    [Fact]
    public void DeserializeObject_WhenPropertyHasPrivateSetter_ShouldSetValue()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new PrivatePropertySetterResolver() };

        var result = JsonConvert.DeserializeObject<SampleWithPrivateSetter>("{\"Name\":\"alpha\"}", settings);

        Assert.Equal("alpha", result!.Name);
    }

    public sealed class SampleWithPrivateSetter
    {
        public string Name { get; private set; } = string.Empty;
    }
}
