using System.Reflection;
using Microsoft.AspNetCore.Components;
using NoTiming.Ui;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Account;
using NTS.Domain.Core.Aggregates;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The pages of the Participations of an Athlete or a Horse (#639, ADR-0010) are for a person who is signed in: a visitor is
/// taken to sign in for the search, as for the profile, and the API answers 401 whatever the Ui shows. What a placing is called
/// is the Results' word for it.
/// </summary>
public sealed class ParticipationsPagesTests
{
    [Theory]
    [InlineData("participations")]
    [InlineData("/participations")]
    [InlineData("/Participations/")]
    [InlineData("PARTICIPATIONS?q=ana")]
    [InlineData("participations#top")]
    public void A_visitor_is_taken_to_sign_in_for_the_search_of_Participations_whatever_the_case_the_slashes_the_query_or_the_fragment(
        string path
    )
    {
        Assert.Equal(RouteAccess.SignIn, WitnessRoutePolicy.AccessTo(null, path));
    }

    [Theory]
    [InlineData("participations/ana")]
    [InlineData("my-participations")]
    [InlineData("events/participations")]
    [InlineData("participation")]
    public void An_address_that_is_not_the_search_of_Participations_is_not_gated(string path)
    {
        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(null, path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_person_has_the_search_of_Participations_whether_or_not_the_profile_is_complete(bool profileComplete)
    {
        var person = new CurrentAccount
        {
            Id = TestId.Of(1),
            Email = "ana@example.test",
            ProfileComplete = profileComplete,
        };

        Assert.Equal(RouteAccess.Open, WitnessRoutePolicy.AccessTo(person, "participations"));
    }

    [Fact]
    public void Every_route_of_the_page_of_the_search_is_one_a_visitor_is_taken_to_sign_in_for()
    {
        var routes = typeof(ParticipationsContent)
            .GetCustomAttributes<RouteAttribute>()
            .Select(x => x.Template)
            .ToList();

        Assert.Equal([Routes.PARTICIPATIONS_PAGE], routes);
        Assert.All(routes, route => Assert.Equal(RouteAccess.SignIn, WitnessRoutePolicy.AccessTo(null, route)));
    }

    [Fact]
    public void A_placing_is_its_rank_the_elimination_with_its_code_or_not_ranked_and_none_is_nothing()
    {
        Assert.Equal("3", PlacingText.Of(Placing.Ranked(3)));
        Assert.Equal($"{Eliminated_string}: FTQ GA", PlacingText.Of(Placing.Eliminated("FTQ GA")));
        Assert.Equal(Not_ranked_string, PlacingText.Of(Placing.NotRanked()));
        Assert.Equal(string.Empty, PlacingText.Of(null));
    }
}
