using NoTiming.Api.Features.Live;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The announcements of changed Participations (ADR-0006), kept for a test to read back: it takes the place of the hub's, so
/// that what the Api announced, and what it did not, is seen without a connected viewer. An announcement is made once, after
/// the write that it belongs to was stored.
/// </summary>
internal sealed class RecordedChanges : IParticipationChanges
{
    public List<(Guid EventId, Guid ParticipationId)> Announced { get; } = [];

    public Task AnnounceAsync(Guid eventId, Guid participationId)
    {
        lock (Announced)
        {
            Announced.Add((eventId, participationId));
        }

        return Task.CompletedTask;
    }
}
