using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.Behinds.Adapters;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Notify;
using NoTiming.Ui.Storage.REST;
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

    readonly BrandingApiRepository _branding;
    readonly IConsoleTenant _tenant;
    readonly INotifier _notifier;
    Logos _printed = Logos.None;
    Guid? _shown;

    public ConsoleBranding(BrandingApiRepository branding, IConsoleTenant tenant, INotifier notifier)
    {
        _branding = branding;
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
        return Read(await ApiRefusals.Answered(() => _branding.OfTenant(tenantId)));
    }

    public async Task<Logos> OfEvent(Guid eventId)
    {
        return Read(await ApiRefusals.Answered(() => _branding.OfEvent(eventId)));
    }

    public async Task SetOfTenant(LogoPosition position, string contentType, byte[] image)
    {
        var tenantId = await TenantId();
        await Change(position, Checked(contentType, image), logo => _branding.ChangeOfTenant(tenantId, logo));
    }

    public async Task ClearOfTenant(LogoPosition position)
    {
        var tenantId = await TenantId();
        await Change(position, null, logo => _branding.ChangeOfTenant(tenantId, logo));
    }

    public async Task SetOfEvent(Guid eventId, LogoPosition position, string contentType, byte[] image)
    {
        await Change(position, Checked(contentType, image), logo => _branding.ChangeOfEvent(eventId, logo));
    }

    public async Task ClearOfEvent(Guid eventId, LogoPosition position)
    {
        await Change(position, null, logo => _branding.ChangeOfEvent(eventId, logo));
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

    /// <summary>The logos of a resource the Api answered with: what is printed for each position, none when it has none.</summary>
    static Logos Read(JsonElement resource)
    {
        var attributes = resource.GetProperty("attributes");
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

    /// <summary>Changes the one logo named, and no other: the Api keeps the logo that is not named as it is.</summary>
    async Task Change(LogoPosition position, JsonNode? logo, Func<IReadOnlyDictionary<string, JsonNode?>, Task> change)
    {
        var name = position == LogoPosition.Left ? LEFT : RIGHT;
        await ApiRefusals.Answered(() => change(new Dictionary<string, JsonNode?> { [name] = logo }));
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
