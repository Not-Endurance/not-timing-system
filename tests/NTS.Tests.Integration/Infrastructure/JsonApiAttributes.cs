using System.Text.Json;
using Not.Application.HTTP;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>What a client sends of a model as the attributes of a resource (the rest-api skill).</summary>
internal static class JsonApiAttributes
{
    static readonly string[] SERVER_OWNED = ["id", "tenantId", "isDeleted", "deletedVersion", "version"];

    /// <summary>
    /// The members of the model as the client of the platform writes them, but for the ones the server owns and the ones
    /// named, which the family keeps to itself.
    /// </summary>
    public static Dictionary<string, JsonElement> Of<TModel>(TModel model, params string[] keptByTheFamily)
    {
        var element = JsonSerializer.SerializeToElement(model, JsonApiClient.WriteOptions);
        return element
            .EnumerateObject()
            .Where(x => !SERVER_OWNED.Contains(x.Name) && !keptByTheFamily.Contains(x.Name))
            .ToDictionary(x => x.Name, x => x.Value);
    }

    /// <summary>The same without the members that are named: the document of a row that lacks them.</summary>
    public static Dictionary<string, JsonElement> Without(Dictionary<string, JsonElement> attributes, string member)
    {
        return attributes.Where(x => x.Key != member).ToDictionary(x => x.Key, x => x.Value);
    }
}
