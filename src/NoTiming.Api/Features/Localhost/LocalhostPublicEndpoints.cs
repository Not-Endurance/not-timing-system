#if DEBUG
using NoTiming.Api.Features.Access;

namespace NoTiming.Api.Features.Localhost;

/// <summary>
/// The public endpoints that exist on one host only, and are not on the list of the Api (#607): the ones the localhost
/// bypass adds. They are in a Debug build and nowhere else, so that the list stays the one list of what a deployed host
/// makes public.
/// </summary>
internal sealed class LocalhostPublicEndpoints
{
    public LocalhostPublicEndpoints(IReadOnlyList<PublicEndpoint> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<PublicEndpoint> Entries { get; }
}
#endif
