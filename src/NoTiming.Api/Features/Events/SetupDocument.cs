using NoTiming.Api.Features.Reference;
using NTS.Contracts.Setup.Models;

namespace NoTiming.Api.Features.Events;

/// <summary>What the Setup of an Event shows as a resource, said once for every route that tells it (the Setup's own and the one that makes it).</summary>
internal static class SetupDocument
{
    public static ReferenceMembers<ConfigureEventModel> Members { get; } = new([], []);
}
