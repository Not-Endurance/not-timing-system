using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The form of a Phase (#620) holds each time as the text the person typed, and the domain names a time by the property of the
/// state of the form, so what the domain says of a time is shown at the input that holds it, where the person corrects it.
/// </summary>
public sealed class PhaseUpdateModelTests
{
    [Theory]
    [InlineData(nameof(IPhaseState.StartTime), nameof(PhaseUpdateModel.StartTimeInput))]
    [InlineData(nameof(IPhaseState.ArriveTime), nameof(PhaseUpdateModel.ArriveTimeInput))]
    [InlineData(nameof(IPhaseState.PresentTime), nameof(PhaseUpdateModel.PresentTimeInput))]
    [InlineData(nameof(IPhaseState.RepresentTime), nameof(PhaseUpdateModel.RepresentTimeInput))]
    public void What_is_said_of_a_time_is_shown_at_the_input_that_holds_it(string property, string input)
    {
        Assert.Equal(input, PhaseUpdateModel.InputOf(property));
    }

    [Fact]
    public void A_property_that_no_input_holds_is_its_own()
    {
        Assert.Equal("Gate", PhaseUpdateModel.InputOf("Gate"));
    }
}
