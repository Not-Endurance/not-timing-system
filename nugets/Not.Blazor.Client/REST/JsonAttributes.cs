using System.Text.Json;

namespace Not.Blazor.Client.REST;

/// <summary>What a client reads of the attributes of a resource the Api answered: a flag that is true only when the Api says so, and a text.</summary>
public static class JsonAttributes
{
    public static bool Flag(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.True;
    }

    public static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
