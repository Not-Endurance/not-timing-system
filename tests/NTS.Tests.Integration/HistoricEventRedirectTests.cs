using System.Net;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The pages of the Historic Events were at <c>/past-events</c> until #628 named them as the glossary does. A bookmark of
/// the old address is sent on to the new one, with what it asked for, by the host that serves the Ui, before the Ui is
/// loaded; anybody may follow it, signed in or not.
/// </summary>
public sealed class HistoricEventRedirectTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public HistoricEventRedirectTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData("/past-events", "/historic-events")]
    [InlineData("/past-events?tab=results", "/historic-events?tab=results")]
    [InlineData(
        "/past-events/3f2504e0-4f89-41d3-9a0c-0305e82c3301",
        "/historic-events/3f2504e0-4f89-41d3-9a0c-0305e82c3301"
    )]
    [InlineData(
        "/past-events/3f2504e0-4f89-41d3-9a0c-0305e82c3301?tab=results",
        "/historic-events/3f2504e0-4f89-41d3-9a0c-0305e82c3301?tab=results"
    )]
    public async Task An_address_of_the_past_events_is_sent_on_to_the_historic_events_with_what_it_asked_for(
        string old,
        string expected
    )
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync(old);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task The_new_addresses_are_the_Ui_and_an_old_address_that_is_not_a_page_of_it_is_not_sent_anywhere()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);

        var historic = await client.GetAsync("/historic-events");
        var details = await client.GetAsync("/historic-events/3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        var notAnEvent = await client.GetAsync("/past-events/seven");

        Assert.NotEqual(HttpStatusCode.MovedPermanently, historic.StatusCode);
        Assert.NotEqual(HttpStatusCode.MovedPermanently, details.StatusCode);
        Assert.NotEqual(HttpStatusCode.MovedPermanently, notAnEvent.StatusCode);
        Assert.Equal(historic.StatusCode, notAnEvent.StatusCode);
    }
}
