using System.Text.Json;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>
/// The grants of an Event (#646, ADR-0012): the people the Main Operator linked to its Officials and Operators, by the exact
/// email. The Api decides who may, and says why when it does not (an Event that has ended, an address it cannot read, the
/// limit of grants an Event has): what it says is thrown as a domain error, so that the form that asked shows it and keeps
/// what was typed.
/// </summary>
public class ConsoleGrants : IConsoleGrants, ITransient
{
    const string GRANTS = "event-grants";

    readonly JsonApiClient _api;

    public ConsoleGrants(JsonApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<GrantRow>> List(Guid eventId)
    {
        var filter = Uri.EscapeDataString($"eventId eq {eventId}");
        var response = Accepted(await _api.Send(HttpMethod.Get, $"{GRANTS}?filter={filter}"));
        return [.. response.Document!.Value.GetProperty("data").EnumerateArray().Select(RowOf)];
    }

    public async Task<GrantRow> Link(Guid eventId, GrantKind kind, OfficialRole? role, string email)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["eventId"] = eventId,
            ["kind"] = kind.ToString().ToLowerInvariant(),
            ["email"] = email.Trim(),
        };
        if (kind == GrantKind.Official)
        {
            attributes["officialRole"] = (role ?? throw new DomainException(Select_a_role_string)).ToString();
        }

        var document = JsonSerializer.SerializeToElement(
            new { data = new { type = GRANTS, attributes } },
            JsonApiClient.Options
        );
        var response = Accepted(await _api.Send(HttpMethod.Post, GRANTS, document));
        return RowOf(response.Document!.Value.GetProperty("data"));
    }

    public async Task Remove(Guid grantId)
    {
        Accepted(await _api.Send(HttpMethod.Delete, $"{GRANTS}/{grantId}"));
    }

    static GrantRow RowOf(JsonElement resource)
    {
        var attributes = resource.GetProperty("attributes");
        var kind = Enum.Parse<GrantKind>(attributes.GetProperty("kind").GetString()!, ignoreCase: true);
        return new GrantRow
        {
            Id = Guid.Parse(resource.GetProperty("id").GetString()!),
            Kind = kind,
            Role = Text(attributes, "officialRole") is { } role
                ? Enum.Parse<OfficialRole>(role, ignoreCase: false)
                : null,
            IsPending =
                attributes.TryGetProperty("pending", out var pending) && pending.ValueKind == JsonValueKind.True,
            DisplayName = Text(attributes, "displayName"),
            Email = Text(attributes, "email") ?? string.Empty,
        };
    }

    static JsonApiResponse Accepted(JsonApiResponse response)
    {
        return response.IsSuccess
            ? response
            : throw new DomainException(response.Error?.Message ?? response.Status.ToString());
    }

    static string? Text(JsonElement attributes, string member)
    {
        return attributes.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
