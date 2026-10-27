using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Branding;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>The members a change of a Setup names. Its id, when the document gives one, is the one of the route.</summary>
internal sealed class SetupChange : MemberAttributes, IResourceAttributes
{
    public static string? ResourceType => "configure-events";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;
}
