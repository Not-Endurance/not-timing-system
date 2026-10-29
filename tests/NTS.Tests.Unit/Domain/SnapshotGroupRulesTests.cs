using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The rules of a group that was sent (#645, ADR-0013): which answers are only for now, whether the answers are final, and
/// whether a group is sent again as the group it was.
/// </summary>
public sealed class SnapshotGroupRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public void A_request_that_did_not_arrive_or_was_answered_for_now_is_only_for_now(int status)
    {
        Assert.True(SnapshotGroupRules.IsOnlyForNow(new SnapshotAnswer(false, status, null)));
    }

    [Fact]
    public void A_busy_Participation_is_only_for_now()
    {
        Assert.True(SnapshotGroupRules.IsOnlyForNow(new SnapshotAnswer(false, 409, "participation-busy")));
    }

    [Theory]
    [InlineData(401, null)]
    [InlineData(403, null)]
    [InlineData(409, "event-ended")]
    [InlineData(404, "participation-not-found")]
    public void A_refusal_that_is_the_servers_answer_is_not_only_for_now(int status, string? code)
    {
        Assert.False(SnapshotGroupRules.IsOnlyForNow(new SnapshotAnswer(false, status, code)));
    }

    [Fact]
    public void Answers_are_final_when_a_Snapshot_was_recorded_even_if_another_is_only_for_now()
    {
        Assert.True(
            SnapshotGroupRules.IsFinal([new SnapshotAnswer(true, 201, null), new SnapshotAnswer(false, 503, null)])
        );
    }

    [Fact]
    public void Answers_are_final_when_none_of_them_is_only_for_now()
    {
        Assert.True(
            SnapshotGroupRules.IsFinal(
                [new SnapshotAnswer(false, 403, null), new SnapshotAnswer(false, 409, "event-ended")]
            )
        );
    }

    [Fact]
    public void Answers_are_not_final_when_one_is_only_for_now_and_nothing_was_recorded()
    {
        Assert.False(
            SnapshotGroupRules.IsFinal([new SnapshotAnswer(false, 403, null), new SnapshotAnswer(false, 503, null)])
        );
    }

    [Fact]
    public void A_group_of_the_same_kind_with_the_same_numbers_and_times_is_sent_again_as_itself()
    {
        var kept = new SnapshotGroup([Taken(7, 9, 30), Taken(8, 9, 31)], SnapshotType.Arrive);

        Assert.True(SnapshotGroupRules.IsSentAgain(kept, SnapshotType.Arrive, [Taken(7, 9, 30), Taken(8, 9, 31)]));
    }

    [Fact]
    public void Another_kind_another_time_or_other_Snapshots_are_another_gesture()
    {
        var kept = new SnapshotGroup([Taken(7, 9, 30)], SnapshotType.Arrive);

        Assert.False(SnapshotGroupRules.IsSentAgain(kept, SnapshotType.Present, [Taken(7, 9, 30)]));
        Assert.False(SnapshotGroupRules.IsSentAgain(kept, SnapshotType.Arrive, [Taken(7, 9, 31)]));
        Assert.False(SnapshotGroupRules.IsSentAgain(kept, SnapshotType.Arrive, [Taken(7, 9, 30), Taken(8, 9, 30)]));
    }

    static Snapshot Taken(int number, int hour, int minute)
    {
        return new Snapshot(
            number,
            $"Athlete {number}",
            null,
            new Timestamp(new DateTimeOffset(2030, 5, 17, hour, minute, 0, TimeSpan.Zero))
        );
    }
}
