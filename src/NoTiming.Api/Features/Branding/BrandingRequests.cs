using System.Text.Json;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Branding;

/// <summary>
/// The change of the logos of a Tenant or an Event: the positions the document names, each with the logo it is given or null to
/// clear it. It reads its own logos, so that a document that cannot be kept is refused before anything is done, and its id, when
/// the document gives one, is the one of the route.
/// </summary>
internal sealed class BrandingChange : MemberAttributes, IResourceAttributes
{
    const string LEFT = "left";
    const string RIGHT = "right";

    Dictionary<LogoPosition, Logo?>? _logos;

    public static string? ResourceType => "brandings";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;

    /// <summary>The logos the document gives, once it is valid.</summary>
    public Dictionary<LogoPosition, Logo?> Logos => _logos ?? [];

    public override JsonApiFailure? Invalid()
    {
        var (changes, failure) = Changes(Members ?? []);
        _logos = changes;
        return failure;
    }

    /// <summary>The positions a document names, each with the logo it is given or none to clear it; the failure that refuses the document when anything in it is not valid.</summary>
    static (Dictionary<LogoPosition, Logo?> Changes, JsonApiFailure? Failure) Changes(
        IReadOnlyDictionary<string, JsonElement> members
    )
    {
        var changes = new Dictionary<LogoPosition, Logo?>();
        foreach (var (name, value) in members)
        {
            var position = PositionOf(name);
            if (position is null)
            {
                return (changes, BrandingFailures.UnsupportedAttribute.With($"{LEFT}, {RIGHT}"));
            }

            if (changes.ContainsKey(position.Value))
            {
                return (changes, BrandingFailures.LogoNamedTwice.With(name));
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                changes[position.Value] = null;
                continue;
            }

            var logo = Logo.Read(value);
            if (logo.Logo is null)
            {
                return (changes, logo.Failure);
            }

            changes[position.Value] = logo.Logo;
        }

        return (changes, null);
    }

    static LogoPosition? PositionOf(string name)
    {
        if (string.Equals(name, LEFT, StringComparison.OrdinalIgnoreCase))
        {
            return LogoPosition.Left;
        }

        return string.Equals(name, RIGHT, StringComparison.OrdinalIgnoreCase) ? LogoPosition.Right : null;
    }
}
