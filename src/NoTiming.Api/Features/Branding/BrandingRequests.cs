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
    Dictionary<LogoPosition, Logo?>? _logos;

    public static string? ResourceType => "brandings";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;

    /// <summary>The logos the document gives, once it is valid.</summary>
    public Dictionary<LogoPosition, Logo?> Logos => _logos ?? [];

    public override JsonApiFailure? Invalid()
    {
        var (changes, failure) = BrandingEndpoints.Changes(Members ?? []);
        _logos = changes;
        return failure;
    }
}
