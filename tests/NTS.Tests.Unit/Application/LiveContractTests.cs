using NTS.Contracts.Live;

namespace NTS.Tests.Unit.Application;

/// <summary>What a viewer is sent (#623, ADR-0013): that a Participation changed, and nothing that describes one.</summary>
public sealed class LiveContractTests
{
    [Fact]
    public void The_only_thing_the_hub_sends_a_viewer_is_that_a_Participation_of_an_Event_changed()
    {
        var sent = Assert.Single(typeof(ILiveClientProcedures).GetMethods());

        Assert.Equal(nameof(ILiveClientProcedures.ParticipationChanged), sent.Name);
        Assert.Equal([typeof(Guid), typeof(Guid)], sent.GetParameters().Select(x => x.ParameterType)); // the Event and the Participation
    }
}
