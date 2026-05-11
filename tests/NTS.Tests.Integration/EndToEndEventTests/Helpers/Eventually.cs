using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal static class Eventually
{
    public static async Task<IReadOnlyList<Participation>> ReadParticipations(
        NexusApiDriver api,
        int eventId,
        Func<IReadOnlyList<Participation>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<Participation> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await api.ReadParticipations(eventId);
            if (predicate(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Nexus API did not reach {expectedState}. Participation count: {last.Count}.");
    }

    public static async Task<Participation> ReadParticipation(
        NexusApiDriver api,
        int eventId,
        int number,
        Func<Participation, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        Participation? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var participations = await api.ReadParticipations(eventId);
            last = participations.FirstOrDefault(x => x.Combination.Number == number);
            if (last != null && predicate(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Participation #{number} did not reach {expectedState}. Last state: {last}.");
    }

    public static async Task<IReadOnlyList<Ranking>> ReadRankings(
        NexusApiDriver api,
        int eventId,
        Func<IReadOnlyList<Ranking>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<Ranking> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await api.ReadRankings(eventId);
            if (predicate(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Rankings did not reach {expectedState}. Ranking count: {last.Count}.");
    }

    public static async Task<IReadOnlyList<Handout>> ReadHandouts(
        NexusApiDriver api,
        int eventId,
        Func<IReadOnlyList<Handout>, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<Handout> last = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await api.ReadHandouts(eventId);
            if (predicate(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Handouts did not reach {expectedState}. Handout count: {last.Count}.");
    }
}
