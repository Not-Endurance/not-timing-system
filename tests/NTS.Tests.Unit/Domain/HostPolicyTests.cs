using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// What the host does by itself, with nobody to ask, to an Event that is no longer Live (#629, ADR-0007). The server
/// refuses every write to such an Event, and the one write it lets through is the finalisation of the placings of its
/// Rankings (#640), which is the host's and no client's. The exemption is one named rule: one operation, at one stage.
/// </summary>
public sealed class HostPolicyTests
{
    [Theory]
    [InlineData(EventStage.Unstarted, false)]
    [InlineData(EventStage.Live, false)]
    [InlineData(EventStage.Historic, true)]
    public void The_placings_of_the_Rankings_are_finalised_once_the_Event_has_ended_and_never_before(
        EventStage stage,
        bool allowed
    )
    {
        Assert.Equal(allowed, HostPolicy.IsAllowed(HostOperation.RankingFinalisation, stage));
    }

    [Fact]
    public void The_finalisation_is_the_one_operation_the_host_may_do_to_an_Event_that_has_ended()
    {
        var allowedAtTheEnd = Enum.GetValues<HostOperation>().Where(x => HostPolicy.IsAllowed(x, EventStage.Historic));

        Assert.Equal([HostOperation.RankingFinalisation], allowedAtTheEnd);
    }

    [Fact]
    public void An_operation_the_policy_has_no_row_for_is_a_mistake_of_the_caller_and_not_a_refusal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HostPolicy.IsAllowed((HostOperation)99, EventStage.Historic));
    }
}
