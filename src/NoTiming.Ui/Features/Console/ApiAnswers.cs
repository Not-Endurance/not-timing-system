using System.Text.Json;
using Not.Application.HTTP;
using Not.Domain.Exceptions;

namespace NoTiming.Ui.Features.Console;

/// <summary>What the services of the Console make of what the Api answers (#646): the answer that took the request or, when it did not, the Api's words as a domain error, and the members of a resource's attributes.</summary>
internal static class ApiAnswers
{
    /// <summary>The answer when the Api took the request; the Api's own words, as a domain error, when it did not.</summary>
    public static JsonApiResponse Accepted(JsonApiResponse response)
    {
        return response.IsSuccess
            ? response
            : throw new DomainException(response.Error?.Message ?? response.Status.ToString());
    }

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
