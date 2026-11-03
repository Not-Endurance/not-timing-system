using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Branding;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>The members a change of a Setup names. Its id, when the document gives one, is the one of the route.</summary>
internal sealed class SetupChange : MemberAttributes, IResourceAttributes
{
    public static string? ResourceType => "configure-events";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;
}

/// <summary>
/// The document of a new Setup: the members it names, which are the ones the Event is made with, and the id the client made
/// for it, which may be left out. A member that is not one of the Setup's is refused where the members are applied.
/// </summary>
internal sealed class NewSetup : MemberAttributes, IResourceAttributes
{
    public static string? ResourceType => "configure-events";

    public static DocumentIds Ids => DocumentIds.OptionalGuid;
}
