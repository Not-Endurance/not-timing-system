using NoTiming.Ui;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Account;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What a page asks of the person before it is shown (#645, ADR-0001, ADR-0012). The read-only pages are public and a
/// visitor has all of them; sending Snapshots and the profile are for a person who is signed in; and only sending
/// Snapshots waits for a complete profile, as gating more would leave a person who is signed in with less than a visitor.
/// </summary>
public sealed class WitnessRoutePolicyTests
{
    static readonly string EVENT = TestId.Of(7).ToString();

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("startlist")]
    [InlineData("arrivelist")]
    [InlineData("presentlist")]
    [InlineData("performance")]
    [InlineData("historic-events")]
    [InlineData("events/{event}/startlist")]
    [InlineData("events/{event}/performance")]
    [InlineData("historic-events/{event}")]
    public void A_visitor_has_every_page_that_only_shows(string path)
    {
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(null, Of(path)));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("events/{event}/snapshot")]
    [InlineData("profile")]
    public void A_visitor_is_taken_to_sign_in_for_the_pages_that_send_Snapshots_and_the_profile(string path)
    {
        Assert.Equal(RouteAccess.SignIn, WitnessRoutePolicy.AccessTo(null, Of(path)));
    }

    [Theory]
    [InlineData("snapshot/")]
    [InlineData("/snapshot")]
    [InlineData("SNAPSHOT")]
    [InlineData("snapshot?sort=number")]
    [InlineData("snapshot#history")]
    [InlineData("Events/{event}/Snapshot/?x=1#top")]
    [InlineData("/Profile")]
    public void The_page_is_known_by_its_path_whatever_the_case_the_slashes_the_query_or_the_fragment(string path)
    {
        Assert.Equal(RouteAccess.SignIn, WitnessRoutePolicy.AccessTo(null, Of(path)));
    }

    [Theory]
    [InlineData("snapshots")]
    [InlineData("snapshot/history")]
    [InlineData("events/snapshot")]
    [InlineData("events/not-an-id/snapshot")]
    [InlineData("events/{event}/snapshot/more")]
    [InlineData("events/{event}")]
    [InlineData("profile/other")]
    [InlineData("my-profile")]
    public void An_address_that_is_not_one_of_those_pages_is_not_gated(string path)
    {
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(null, Of(path)));
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(Account(profileComplete: false), Of(path)));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("events/{event}/snapshot")]
    [InlineData("profile")]
    [InlineData("startlist")]
    [InlineData("")]
    public void A_person_with_a_complete_profile_has_every_page(string path)
    {
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(Account(profileComplete: true), Of(path)));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("events/{event}/snapshot")]
    [InlineData("/Snapshot/?x=1")]
    public void A_person_without_a_complete_profile_is_taken_to_complete_it_before_sending_Snapshots(string path)
    {
        Assert.Equal(RouteAccess.Profile, WitnessRoutePolicy.AccessTo(Account(profileComplete: false), Of(path)));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("startlist")]
    [InlineData("arrivelist")]
    [InlineData("events/{event}/performance")]
    [InlineData("historic-events")]
    [InlineData("")]
    public void A_person_without_a_complete_profile_still_has_the_profile_and_every_page_that_only_shows(string path)
    {
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(Account(profileComplete: false), Of(path)));
    }

    static string Of(string path)
    {
        return path.Replace("{event}", EVENT);
    }

    static CurrentAccount Account(bool profileComplete)
    {
        return new CurrentAccount
        {
            Id = TestId.Of(1),
            Email = "ana@example.test",
            ProfileComplete = profileComplete,
        };
    }
}
