using Not.Application.Behinds.Adapters;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Startlists;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal static class CoreAssertions
{
    public static async Task AssertStartlistsMatchPersisted(
        NexusApiDriver api,
        JudgeDriver judge,
        WitnessDriver witness,
        int eventId
    )
    {
        var expected = new Startlist(await api.ReadParticipations(eventId));
        var judgeUpcoming = judge.GetRequiredService<IStartUpcoming>();
        var judgeHistory = judge.GetRequiredService<IStartHistory>();
        var witnessUpcoming = witness.GetRequiredService<IStartUpcoming>();
        var witnessHistory = witness.GetRequiredService<IStartHistory>();

        await Reload(judgeUpcoming);
        await Reload(judgeHistory);
        await Reload(witnessUpcoming);
        await Reload(witnessHistory);

        Assert.Equal(expected.Upcoming.Select(x => x.Number), judgeUpcoming.Upcoming.Select(x => x.Number));
        Assert.Equal(expected.Upcoming.Select(x => x.Number), witnessUpcoming.Upcoming.Select(x => x.Number));
        Assert.Equal(Flatten(expected.HistoryByStage), Flatten(judgeHistory.HistoryByStage));
        Assert.Equal(Flatten(expected.HistoryByStage), Flatten(witnessHistory.HistoryByStage));
    }

    public static void AssertPhaseMatches(Phase expected, Phase actual)
    {
        AssertEqualTimeOfDay(expected.StartTime, actual.StartTime);
        AssertEqualTimeOfDay(expected.ArriveTime, actual.ArriveTime);
        AssertEqualTimeOfDay(expected.PresentTime, actual.PresentTime);
        AssertEqualTimeOfDay(expected.GetRequiredInspectionTime(), actual.GetRequiredInspectionTime());
        AssertEqualTimeOfDay(expected.GetOutTime(), actual.GetOutTime());
        Assert.Equal(expected.GetLoopInterval(), actual.GetLoopInterval());
        Assert.Equal(expected.GetPhaseInterval(), actual.GetPhaseInterval());
        Assert.Equal(expected.GetRecoveryInterval(), actual.GetRecoveryInterval());
        Assert.Equal(expected.GetAverageLoopSpeed(), actual.GetAverageLoopSpeed());
        Assert.Equal(expected.GetAveragePhaseSpeed(), actual.GetAveragePhaseSpeed());
        Assert.Equal(expected.GetAverageSpeed(), actual.GetAverageSpeed());
        Assert.Equal(expected.IsComplete(), actual.IsComplete());
    }

    public static void AssertEqualTimeOfDay(DateTimeOffset expected, Timestamp? actual)
    {
        Assert.True(
            SameTimeOfDay(actual, expected),
            $"Expected time {expected:HH:mm:ss}, got {actual?.ToDateTimeOffset():HH:mm:ss}."
        );
    }

    public static void AssertEqualTimeOfDay(Timestamp? expected, Timestamp? actual)
    {
        if (expected == null || actual == null)
        {
            Assert.Equal(expected, actual);
            return;
        }

        Assert.Equal(expected.ToDateTimeOffset().TimeOfDay, actual.ToDateTimeOffset().TimeOfDay);
    }

    public static bool SameTimeOfDay(Timestamp? actual, DateTimeOffset expected)
    {
        return actual?.ToDateTimeOffset().TimeOfDay == expected.TimeOfDay;
    }

    static IReadOnlyList<string> Flatten(IReadOnlyDictionary<int, IReadOnlyList<Starter>> starters)
    {
        return starters.SelectMany(x => x.Value.Select(starter => $"{x.Key}:{starter.Number}")).ToArray();
    }

    static async Task Reload(IStatefulService service)
    {
        if (service is NStatefulService stateful)
        {
            stateful.ResetHasLoaded();
        }

        await service.Load();
    }
}
