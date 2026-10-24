using MongoDB.Bson;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: how the report names the Events a copy is in, so that a review can tell a Spring ride from an Autumn ride.</summary>
public sealed class IdentityEventsTests
{
    [Fact]
    public void An_Event_is_named_by_its_dates_and_its_name_the_dates_first_so_that_they_sort_by_time()
    {
        var labels = IdentityEvents.Labels(
            [
                Information(900, "Spring ride", StartOf(4, 26), EndOf(4, 28)),
                Information(901, "Autumn ride", StartOf(9, 1), EndOf(9, 1)),
            ],
            []
        );

        Assert.Equal("2026-04-26 to 2026-04-28 Spring ride", labels["900"]);
        Assert.Equal("2026-09-01 Autumn ride", labels["901"]);
    }

    [Fact]
    public void The_days_are_the_days_of_the_place_and_not_the_days_of_the_machine_they_are_read_on()
    {
        var labels = IdentityEvents.Labels(
            [
                Information(900, "East", new DateTime(2026, 4, 26, 0, 0, 0, DateTimeKind.Utc).AddHours(-10), null),
                Information(901, "West", new DateTime(2026, 4, 26, 0, 0, 0, DateTimeKind.Utc).AddHours(5), null),
            ],
            []
        );

        Assert.Equal("2026-04-26 East", labels["900"]);
        Assert.Equal("2026-04-26 West", labels["901"]);
    }

    [Fact]
    public void The_last_day_is_the_day_of_the_place_too_west_of_Greenwich_where_its_last_second_is_after_midnight_in_UTC()
    {
        var labels = IdentityEvents.Labels(
            [
                Information(
                    900,
                    "West",
                    new DateTime(2026, 4, 26, 5, 0, 0, DateTimeKind.Utc), // the first second of the 26th at UTC-5
                    new DateTime(2026, 4, 29, 4, 59, 59, DateTimeKind.Utc) // the last second of the 28th at UTC-5
                ),
            ],
            []
        );

        Assert.Equal("2026-04-26 to 2026-04-28 West", labels["900"]);
    }

    [Fact]
    public void An_Event_with_one_date_or_none_is_named_with_what_it_has()
    {
        var labels = IdentityEvents.Labels(
            [Information(900, "Spring ride", start: StartOf(4, 26)), Information(901, "Autumn ride")],
            []
        );

        Assert.Equal("2026-04-26 Spring ride", labels["900"]);
        Assert.Equal("Autumn ride", labels["901"]);
    }

    [Fact]
    public void A_setup_with_no_Event_of_its_own_is_named_by_its_name_and_the_start_of_its_first_Competition()
    {
        var setup = new BsonDocument
        {
            ["_id"] = 902,
            ["Name"] = "Winter ride",
            ["Competitions"] = new BsonArray
            {
                new BsonDocument { ["Start"] = new BsonDateTime(new DateTime(2026, 12, 5, 7, 0, 0, DateTimeKind.Utc)) },
                new BsonDocument { ["Start"] = new BsonDateTime(new DateTime(2026, 12, 4, 7, 0, 0, DateTimeKind.Utc)) },
            },
        };

        var labels = IdentityEvents.Labels([], [setup]);

        Assert.Equal("2026-12-04 Winter ride", labels["902"]);
    }

    [Fact]
    public void The_Event_that_has_started_is_named_by_its_own_document_and_not_by_the_setup_it_came_from()
    {
        var setup = new BsonDocument { ["_id"] = 900, ["Name"] = "Setup name" };

        var labels = IdentityEvents.Labels([Information(900, "Spring ride", StartOf(4, 26), EndOf(4, 26))], [setup]);

        Assert.Equal("2026-04-26 Spring ride", Assert.Single(labels).Value);
    }

    [Fact]
    public void An_Event_is_found_by_the_id_the_copies_name_it_by_a_Guid_as_text_or_a_number_as_text()
    {
        var guid = Guid.NewGuid();
        var labels = IdentityEvents.Labels(
            [
                Information(
                    new BsonBinaryData(guid, GuidRepresentation.Standard),
                    "Spring ride",
                    StartOf(4, 26),
                    EndOf(4, 26)
                ),
            ],
            []
        );

        Assert.Equal("2026-04-26 Spring ride", labels[guid.ToString()]);
    }

    [Fact]
    public void A_document_without_a_name_is_left_out_and_a_date_that_is_not_one_is_ignored()
    {
        var unnamed = new BsonDocument { ["_id"] = 903 };
        var odd = Information(904, "Odd ride");
        odd["StartDay"] = "tomorrow";

        var labels = IdentityEvents.Labels([unnamed, odd], []);

        Assert.False(labels.ContainsKey("903"));
        Assert.Equal("Odd ride", labels["904"]);
    }

    [Fact]
    public void The_date_of_an_Event_is_the_instant_it_starts_and_that_of_a_Setup_the_start_of_its_first_Competition()
    {
        var dates = IdentityEvents.Dates(
            [
                Information(900, "Spring ride", StartOf(4, 26), EndOf(4, 28)),
                new BsonDocument { ["_id"] = 902, ["EndDay"] = new BsonDateTime(EndOf(5, 1)) },
            ],
            [
                new BsonDocument
                {
                    ["_id"] = 903,
                    ["Competitions"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["Start"] = new BsonDateTime(new DateTime(2026, 12, 5, 7, 0, 0, DateTimeKind.Utc)),
                        },
                        new BsonDocument
                        {
                            ["Start"] = new BsonDateTime(new DateTime(2026, 12, 4, 7, 0, 0, DateTimeKind.Utc)),
                        },
                        new BsonDocument(),
                    },
                },
                new BsonDocument { ["_id"] = 904, ["Competitions"] = new BsonArray() },
                new BsonDocument
                {
                    ["_id"] = 900, // an Event that has started is told by its own document
                    ["Competitions"] = new BsonArray
                    {
                        new BsonDocument { ["Start"] = new BsonDateTime(StartOf(1, 1)) },
                    },
                },
            ]
        );

        Assert.Equal(StartOf(4, 26), dates["900"]);
        Assert.Equal(EndOf(5, 1), dates["902"]); // one that has no start is told by its end
        Assert.Equal(new DateTime(2026, 12, 4, 7, 0, 0, DateTimeKind.Utc), dates["903"]);
        Assert.False(dates.ContainsKey("904"));
    }

    static BsonDocument Information(BsonValue id, string name, DateTime? start = null, DateTime? end = null)
    {
        var information = new BsonDocument { ["_id"] = id, ["Name"] = name };
        if (start != null)
        {
            information["StartDay"] = new BsonDateTime(start.Value);
        }

        if (end != null)
        {
            information["EndDay"] = new BsonDateTime(end.Value);
        }

        return information;
    }

    /// <summary>The first instant of a day in Sofia in summer, which is how the days of an Event are stored.</summary>
    static DateTime StartOf(int month, int day)
    {
        return new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Utc).AddHours(-3);
    }

    /// <summary>The last second of a day in Sofia in summer.</summary>
    static DateTime EndOf(int month, int day)
    {
        return new DateTime(2026, month, day, 23, 59, 59, DateTimeKind.Utc).AddHours(-3);
    }
}
