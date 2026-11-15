using MongoDB.Bson;
using NC.Serialization.JSON;
using NC.Storage.Mongo;
using Newtonsoft.Json;

namespace NoTiming.IntegrationTests;

public sealed class MongoSerializationTests
{
    [Fact]
    public void JsonSettings_OmitNullValues()
    {
        var json = JsonConvert.SerializeObject(new SerializationSample { Name = "Saved" }, new NJsonSettings());

        Assert.Contains("\"Name\"", json);
        Assert.DoesNotContain("Optional", json);
    }

    [Fact]
    public void Register_OmitsNullAndDefaultValues()
    {
        MongoConventions.Register();

        var document = new SerializationSample { Name = "Saved" }.ToBsonDocument();

        Assert.Equal("Saved", document["Name"].AsString);
        Assert.False(document.Contains("Optional"));
        Assert.False(document.Contains("Count"));
        Assert.False(document.Contains("Enabled"));
    }

    sealed class SerializationSample
    {
        public string Name { get; set; } = default!;
        public string? Optional { get; set; }
        public int Count { get; set; }
        public bool Enabled { get; set; }
    }
}
