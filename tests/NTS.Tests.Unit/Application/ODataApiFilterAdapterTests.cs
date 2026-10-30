using System.Globalization;
using System.Linq.Expressions;
using Not.Application.HTTP;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Unit.Application;

public class ODataApiFilterAdapterTests
{
    static readonly Guid AN_EVENT = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
    static readonly Guid ANOTHER_EVENT = Guid.Parse("9b2f1c64-7d0e-4a5b-8c31-5e6f7a8b9c0d");
    static readonly Guid A_PARTICIPATION = Guid.Parse("c1d2e3f4-0a1b-4c2d-8e3f-a4b5c6d7e8f9");

    public static TheoryData<Expression<Func<Sample, bool>>, string> Comparisons()
    {
        return new()
        {
            { x => x.Number == 5, "Number eq 5" },
            { x => x.Number != 5, "Number ne 5" },
            { x => x.Number > 5, "Number gt 5" },
            { x => x.Number >= 5, "Number ge 5" },
            { x => x.Number < 5, "Number lt 5" },
            { x => x.Number <= 5, "Number le 5" },
            { x => 5 < x.Number, "Number gt 5" },
            { x => 5 >= x.Number, "Number le 5" },
            { x => "A" == x.Name, "Name eq 'A'" },
        };
    }

    public static TheoryData<Expression<Func<Sample, bool>>> Untranslatable()
    {
        List<SampleKind> kinds = [SampleKind.Operator];
        return new()
        {
            { x => x.Name!.StartsWith("A") },
            { x => x.Name == "A" || x.Name == "B" },
            { x => x.Country!.Name == "Bulgaria" },
            { x => x.Number + 1 > 5 },
            { x => x.Kind == SampleKind.Official },
            { x => x.Kind != SampleKind.Official },
            { x => kinds.Contains(x.Kind) },
        };
    }

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

    [Fact]
    public void A_string_is_quoted_and_a_quote_in_it_is_doubled()
    {
        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.Name == "O'Brien Club"]);

        Assert.Equal("Name eq 'O''Brien Club'", filters["$filter"]);
    }

    [Fact]
    public void A_number_is_written_the_same_in_every_culture()
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("bg-BG");
        try
        {
            var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.Speed >= 12.5]);

            Assert.Equal("Speed ge 12.5", filters["$filter"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void A_boolean_member_and_a_null_are_written_as_OData_writes_them()
    {
        var flag = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.IsNotRanked]);
        var isNull = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.NameEnglish == null]);

        var negated = ODataApiFilterAdapter.ParseFilters<Sample>([x => !x.IsNotRanked]);

        Assert.Equal("IsNotRanked eq true", flag["$filter"]);
        Assert.Equal("IsNotRanked eq false", negated["$filter"]);
        Assert.Equal("NameEnglish eq null", isNull["$filter"]);
    }

    [Theory]
    [MemberData(nameof(Comparisons))]
    public void Every_comparison_of_a_member_with_a_value_is_written_with_its_OData_operator(
        Expression<Func<Sample, bool>> comparison,
        string expected
    )
    {
        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([comparison]);

        Assert.Equal(expected, filters["$filter"]);
    }

    [Fact]
    public void A_list_with_nothing_in_it_matches_nothing_and_a_list_of_one_is_a_comparison()
    {
        List<Guid> none = [];
        List<Guid> one = [AN_EVENT];

        var empty = ODataApiFilterAdapter.ParseFilters<Sample>([x => none.Contains(x.EventId)]);
        var single = ODataApiFilterAdapter.ParseFilters<Sample>([x => one.Contains(x.EventId)]);

        Assert.Equal("false", empty["$filter"]);
        Assert.Equal("EventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301", single["$filter"]);
    }

    [Fact]
    public void A_condition_that_is_always_true_asks_for_no_filter_at_all()
    {
        var parsed = ODataApiFilterAdapter.TryParseFilters<Sample>([x => true], out var query);

        Assert.True(parsed);
        Assert.Empty(query);
    }

    [Theory]
    [MemberData(nameof(Untranslatable))]
    public void An_expression_the_adapter_cannot_write_is_refused_so_that_the_repository_filters_for_itself(
        Expression<Func<Sample, bool>> expression
    )
    {
        var parsed = ODataApiFilterAdapter.TryParseFilters<Sample>([expression], out var query);

        Assert.False(parsed);
        Assert.Empty(query);
        Assert.Throws<NotSupportedException>(() => ODataApiFilterAdapter.ParseFilters<Sample>([expression]));
    }

    [Fact]
    public void The_JSON_API_variant_names_the_members_as_the_resource_does_in_camelCase()
    {
        List<Guid> events = [AN_EVENT, ANOTHER_EVENT];

        var filters = ODataApiFilterAdapter.ParseFilters<Sample>(
            [
                x => x.EventId == AN_EVENT && x.IsNotRanked == false && x.NameEnglish == null,
                x => events.Contains(x.EventId),
            ],
            camelCase: true
        );

        Assert.Equal(
            "(eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 and isNotRanked eq false and nameEnglish eq null) and ((eventId eq 3f2504e0-4f89-41d3-9a0c-0305e82c3301 or eventId eq 9b2f1c64-7d0e-4a5b-8c31-5e6f7a8b9c0d))",
            filters["$filter"]
        );
    }

    [Fact]
    public void Without_the_option_the_members_keep_the_names_they_have()
    {
        var filters = ODataApiFilterAdapter.ParseFilters<Sample>([x => x.NameEnglish == null]);

        Assert.Equal("NameEnglish eq null", filters["$filter"]);
    }

    public sealed class Sample
    {
        public Guid EventId { get; set; }
        public Guid? UserId { get; set; }
        public bool IsNotRanked { get; set; }
        public int Number { get; set; }
        public double Speed { get; set; }
        public string? Name { get; set; }
        public string? NameEnglish { get; set; }
        public SampleKind Kind { get; set; }
        public Sample? Country { get; set; }
    }

    public enum SampleKind
    {
        Operator = 1,
        Official = 2,
    }
}
