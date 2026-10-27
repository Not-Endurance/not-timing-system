using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Events;

/// <summary>What a Console Operator is sent as: an Event, and a person by an email or an account. A member that is not one of these is kept, to be refused.</summary>
internal sealed class ConsoleOperatorAttributes : IResourceAttributes, INamesEvent
{
    public static string? ResourceType => "console-operators";

    public string? EventId { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    public JsonApiFailure? Invalid()
    {
        if (Others is { Count: > 0 })
        {
            return EventsFailures.ConsoleOperatorUnsupportedAttribute.With(string.Join(", ", Others.Keys));
        }

        if ((Email == null) == (AccountId == null))
        {
            return EventsFailures.MalformedRequest;
        }

        return Email != null && !CodeSignIn.IsValidEmail(Email) ? EventsFailures.InvalidEmail : null;
    }
}
