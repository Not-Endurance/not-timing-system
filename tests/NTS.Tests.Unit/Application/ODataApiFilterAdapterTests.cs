using Not.Application.HTTP;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Unit.Application;

public class ODataApiFilterAdapterTests
{
    static readonly Guid AN_EVENT = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
    static readonly Guid ANOTHER_EVENT = Guid.Parse("9b2f1c64-7d0e-4a5b-8c31-5e6f7a8b9c0d");
    static readonly Guid A_PARTICIPATION = Guid.Parse("c1d2e3f4-0a1b-4c2d-8e3f-a4b5c6d7e8f9");

    [Fact]
    public void A_Guid_comparison_is_written_as_an_unquoted_literal()
    {
        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.EventId == AN_EVENT]);

        Assert.Equal("EventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301", filters["$filter"]);
    }

    [Fact]
    public void A_nullable_Guid_is_written_the_same_way()
    {
        Guid? userId = AN_EVENT;

        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.UserId == userId]);

        Assert.Equal("UserId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301", filters["$filter"]);
    }

    [Fact]
    public void A_list_of_Guids_becomes_a_disjunction_of_unquoted_literals()
    {
        List<Guid> events = [AN_EVENT, ANOTHER_EVENT];

        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => events.Contains(x.EventId)]);

        Assert.Equal(
            "(EventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 or EventId eq 9b2f1c64-7d0e-4a5b-8c31-5e6f7a8b9c0d)",
            filters["$filter"]
        );
    }

    [Fact]
    public void A_Guid_filter_combines_with_the_other_conditions_and_the_scope()
    {
        var filters = ODataApiFilterAdapter.ParseFilters<Sample>(
            [x => x.EventId == AN_EVENT && x.IsNotRanked == false, x => x.Number > 3]
        );

        Assert.Equal(
            "(EventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 and IsNotRanked eq false) and (Number gt 3)",
            filters["$filter"]
        );
        Assert.DoesNotContain("'", filters["$filter"]);
    }

    [Fact]
    public void The_Handouts_of_a_Participation_are_asked_for_by_a_filter_the_server_applies()
    {
        var parsed = ODataApiFilterAdapter.TryParseFilters<Handout>(
            [x => x.ParticipationId == A_PARTICIPATION],
            out var query
        );

        Assert.True(parsed); // when it cannot be parsed the repository reads every Handout and filters here
        Assert.Equal("ParticipationId eq c1d2e3f4-0a1b-4c2d-8e3f-a4b5c6d7e8f9", query["$filter"]);
    }

    sealed class Sample
    {
        public Guid EventId { get; set; }
        public Guid? UserId { get; set; }
        public bool IsNotRanked { get; set; }
        public int Number { get; set; }
    }
}
