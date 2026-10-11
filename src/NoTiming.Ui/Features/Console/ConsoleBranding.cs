using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.Behinds.Adapters;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Notify;
using NTS.Contracts.Branding;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The logos of the Console (#646, ADR-0012): what the Api says an Event is printed with, which the pages that print put at the
/// head of their documents, and the editing of the Tenant's logos and of an Event's. A logo is checked here for its kind and its
/// weight before it is sent, so that a person who chose a file that no server would take is told so at once; the Api checks it
/// again, by what the file begins with, and what it refuses is said in its words. A page that cannot read the logos prints
/// without them and says so: the documents do not wait for a logo.
/// </summary>
public class ConsoleBranding : NStatefulService, IConsoleBranding, IScoped
{
    const string LEFT = "left";
    const string RIGHT = "right";

    readonly JsonApiClient _api;
    readonly IConsoleTenant _tenant;
    readonly INotifier _notifier;
    Logos _printed = Logos.None;
    Guid? _shown;

    public ConsoleBranding(JsonApiClient api, IConsoleTenant tenant, INotifier notifier)
    {
        _api = api;
        _tenant = tenant;
        _notifier = notifier;
    }

    public string? LeftLogo => _printed.Left?.DataUrl;

    public string? RightLogo => _printed.Right?.DataUrl;

    public async Task Show(IViewedEvent view)
    {
        _shown = view.Event.Id;
        await ReadPrinted(view.Event.Id);
        EmitChanged();
    }

    public async Task<Logos> OfTenant()
    {
        var tenantId = await TenantId();
        return Read(Accepted(await _api.Send(HttpMethod.Get, $"tenants/{Uri.EscapeDataString(tenantId)}/branding")));
    }

    public async Task<Logos> OfEvent(Guid eventId)
    {
        return Read(Accepted(await _api.Send(HttpMethod.Get, $"events/{eventId}/branding")));
    }

    public async Task SetOfTenant(LogoPosition position, string contentType, byte[] image)
    {
        await Change(
            $"tenants/{Uri.EscapeDataString(await TenantId())}/branding",
            position,
            Checked(contentType, image)
        );
    }

    public async Task ClearOfTenant(LogoPosition position)
    {
        await Change($"tenants/{Uri.EscapeDataString(await TenantId())}/branding", position, null);
    }

    public async Task SetOfEvent(Guid eventId, LogoPosition position, string contentType, byte[] image)
    {
        await Change($"events/{eventId}/branding", position, Checked(contentType, image));
    }

    public async Task ClearOfEvent(Guid eventId, LogoPosition position)
    {
        await Change($"events/{eventId}/branding", position, null);
    }

    /// <summary>The answer when the Api took the request; the Api's own words, as a domain error, when it did not.</summary>
    static JsonElement Accepted(JsonApiResponse response)
    {
        return response is { IsSuccess: true, Document: { } document }
            ? document
            : throw new DomainException(response.Error?.Message ?? response.Status.ToString());
    }

    static JsonNode Checked(string contentType, byte[] image)
    {
        var kind = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (image.Length == 0 || !LogoLimits.CONTENT_TYPES.Contains(kind))
        {
            throw new DomainException(Logo_not_an_image_string);
        }

        if (image.Length > LogoLimits.MAX_BYTES)
        {
            throw new DomainException(string.Format(Logo_is_too_large_string, LogoLimits.MAX_BYTES / 1024));
        }

        return new JsonObject { ["contentType"] = kind, ["content"] = Convert.ToBase64String(image) };
    }

    static Logos Read(JsonElement document)
    {
        var attributes = document.GetProperty("data").GetProperty("attributes");
        return new Logos(LogoOf(attributes, LEFT), LogoOf(attributes, RIGHT));
    }

    static Logo? LogoOf(JsonElement attributes, string position)
    {
        if (!attributes.TryGetProperty(position, out var logo) || logo.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var source =
            logo.TryGetProperty("source", out var named) && named.ValueKind == JsonValueKind.String
                ? Enum.Parse<LogoSource>(named.GetString()!, ignoreCase: true)
                : (LogoSource?)null;
        return new Logo(
            logo.GetProperty("contentType").GetString()!,
            Convert.FromBase64String(logo.GetProperty("content").GetString()!),
            source
        );
    }

    /// <summary>The logos of the Event the pages print with, read again; one that cannot be read leaves the pages printing without.</summary>
    async Task ReadPrinted(Guid eventId)
    {
        try
        {
            _printed = await OfEvent(eventId);
        }
        catch (DomainException ex)
        {
            _printed = Logos.None;
            _notifier.Warn(ex.Message);
        }
    }

    async Task Change(string route, LogoPosition position, JsonNode? logo)
    {
        var name = position == LogoPosition.Left ? LEFT : RIGHT;
        var document = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "brandings",
                ["attributes"] = new JsonObject { [name] = logo },
            },
        };
        Accepted(await _api.Send(HttpMethod.Patch, route, JsonSerializer.SerializeToElement(document)));
        if (_shown is { } shown)
        {
            await ReadPrinted(shown); // what is printed with may be what was changed, whether it is the Event's or its Tenant's
            EmitChanged();
        }
    }

    async Task<string> TenantId()
    {
        await _tenant.Load();
        return _tenant.TenantId ?? throw new DomainException(Federation_not_yours_string);
    }
}
