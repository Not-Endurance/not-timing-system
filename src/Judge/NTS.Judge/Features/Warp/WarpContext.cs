using Not.Application.RPC;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public class WarpContext : IRpcMetadata
{
    public string? ConnectionGroupKey { get; private set; }

    public void Configure(UpcomingEvent? upcomingEvent)
    {
        ConnectionGroupKey = upcomingEvent?.Id.ToString();
    }
}
