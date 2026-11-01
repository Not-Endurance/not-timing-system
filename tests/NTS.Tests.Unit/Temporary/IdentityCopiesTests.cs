using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;
using NTS.Tools.IdentityMigration;
using static NTS.Tests.Unit.Temporary.IdentityMigrationDocuments;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: the copies of Athletes and Horses that <c>migrate-identities</c> finds in the documents of each collection.</summary>
public sealed class IdentityCopiesTests
{
    static readonly BsonDocument BULGARIA = Country("Bulgaria", "BG", "BUL");

    [Fact]
    public void A_Setup_row_is_the_document_itself_and_its_integer_id_is_the_legacy_id()
    {
        var row = Person(17, "Иван Петров", "Ivan Petrov", fei: "12345", country: BULGARIA, club: "Sofia");

        var copy = Assert.Single(IdentityCopies.Read("athletes", row));

        Assert.Equal(IdentityKind.Athlete, copy.Kind);
        Assert.True(copy.IsSetupRow);
        Assert.Equal("athletes", copy.Collection);
        Assert.Equal("17", copy.DocumentId);
        Assert.Equal("", copy.Path);
        Assert.Equal("17", copy.LegacyId);
        Assert.Equal("Иван Петров", copy.Name);
        Assert.Equal("Ivan Petrov", copy.NameEnglish);
        Assert.Equal("12345", copy.FeiId);
        Assert.Equal("Bulgaria (BG/BUL)", copy.Country);
        Assert.Equal("Sofia", copy.Club);
        Assert.Null(copy.EventId);
        Assert.Null(copy.ParticipationId);
    }

    [Fact]
    public void A_Setup_Horse_row_is_a_Horse()
    {
        var copy = Assert.Single(IdentityCopies.Read("horses", Person(3, "Burya", fei: "BUL12345")));

        Assert.Equal(IdentityKind.Horse, copy.Kind);
        Assert.True(copy.IsSetupRow);
        Assert.Equal("BUL12345", copy.FeiId);
    }

    [Theory]
    [InlineData(0, null)] // the id of a copy that had none
    [InlineData(42, "42")]
    public void The_id_0_is_no_legacy_id(int id, string? expected)
    {
        var copy = Assert.Single(IdentityCopies.Read("athletes", Person(id, "Ivan Petrov")));

        Assert.Equal(expected, copy.LegacyId);
    }

    [Fact]
    public void An_id_that_is_a_Guid_or_an_object_id_is_no_legacy_id_but_still_names_the_document()
    {
        var guid = Guid.NewGuid();
        var byGuid = Assert.Single(
            IdentityCopies.Read(
                "athletes",
                Person(new BsonBinaryData(guid, GuidRepresentation.Standard), "Ivan Petrov")
            )
        );
        var objectId = ObjectId.GenerateNewId();
        var byObjectId = Assert.Single(IdentityCopies.Read("horses", Person(objectId, "Burya")));

        Assert.Null(byGuid.LegacyId);
        Assert.Equal(guid.ToString(), byGuid.DocumentId);
        Assert.Null(byObjectId.LegacyId);
        Assert.Equal(objectId.ToString(), byObjectId.DocumentId);
    }

    [Fact]
    public void An_id_written_as_text_or_a_long_is_read_as_the_number_it_is()
    {
        var text = Assert.Single(IdentityCopies.Read("athletes", Person("0042", "Ivan Petrov")));
        var big = Assert.Single(IdentityCopies.Read("athletes", Person(new BsonInt64(5_000_000_000), "Ivan Petrov")));

        Assert.Equal("42", text.LegacyId);
        Assert.Equal("5000000000", big.LegacyId);
    }

    [Fact]
    public void An_id_written_as_a_double_is_an_id_only_when_it_is_a_whole_number()
    {
        var whole = Assert.Single(IdentityCopies.Read("athletes", Person(new BsonDouble(42.0), "Ivan Petrov")));
        var fraction = Assert.Single(IdentityCopies.Read("athletes", Person(new BsonDouble(42.5), "Ivan Petrov")));

        Assert.Equal("42", whole.LegacyId);
        Assert.Null(fraction.LegacyId);
    }

    [Fact]
    public void A_person_of_an_older_document_keeps_its_id_under_Id_and_not_under_underscore_id()
    {
        var older = new BsonDocument { ["Id"] = 7, ["Name"] = "Ivan Petrov" };

        var copy = Assert.Single(IdentityCopies.Read("athletes", older));

        Assert.Equal("7", copy.LegacyId);
    }

    [Fact]
    public void A_country_that_has_codes_and_no_name_is_told_by_its_codes()
    {
        var country = new BsonDocument { ["IsoCode"] = "BGR", ["NfCode"] = "BUL" };

        var copy = Assert.Single(IdentityCopies.Read("athletes", Person(1, "A", country: country)));

        Assert.Equal("BGR/BUL", copy.Country);
    }

    [Fact]
    public void A_FEI_ID_is_read_as_text_whatever_it_was_stored_as_and_a_blank_one_is_none()
    {
        var number = Person(2, "B");
        number["FeiId"] = 10234;

        Assert.Equal("10234", Assert.Single(IdentityCopies.Read("athletes", number)).FeiId);
        Assert.Equal("BUL12", Assert.Single(IdentityCopies.Read("horses", Person(1, "A", fei: " BUL12 "))).FeiId);
        Assert.Null(Assert.Single(IdentityCopies.Read("athletes", Person(3, "C", fei: "  "))).FeiId);
    }

    [Fact]
    public void A_country_is_told_as_it_is_stored_by_name_and_codes_so_that_BGR_and_BUL_show_apart()
    {
        var iso = Person(1, "A", country: Country("Bulgaria", "BGR"));
        var nf = Person(2, "B", country: Country("Bulgaria", nfCode: "BUL"));
        var onlyName = Person(3, "C", country: Country("Bulgaria"));
        var typed = Person(4, "D");
        typed["Country"] = "BGR";

        Assert.Equal("Bulgaria (BGR)", Assert.Single(IdentityCopies.Read("athletes", iso)).Country);
        Assert.Equal("Bulgaria (BUL)", Assert.Single(IdentityCopies.Read("athletes", nf)).Country);
        Assert.Equal("Bulgaria", Assert.Single(IdentityCopies.Read("athletes", onlyName)).Country);
        Assert.Equal("BGR", Assert.Single(IdentityCopies.Read("athletes", typed)).Country);
    }

    [Fact]
    public void A_configure_event_holds_copies_in_its_pool_and_in_the_Participations_of_its_Competitions()
    {
        var pooled = Combination(1, Person(11, "Ivan Petrov"), Person(21, "Burya"));
        var ridden = Combination(2, Person(12, "Maria Georgieva"), Person(22, "Vihar"));
        var setup = Setup(900, "Spring ride", [pooled, ridden], ("CEI 1*", 77, ridden));

        var copies = IdentityCopies.Read("configure_events", setup);

        Assert.Equal(6, copies.Count);
        Assert.All(copies, x => Assert.False(x.IsSetupRow));
        Assert.All(copies, x => Assert.Equal("900", x.EventId));
        Assert.All(copies, x => Assert.Equal("900", x.DocumentId));
        Assert.Equal(
            [
                "Combinations[0].Athlete",
                "Combinations[0].Horse",
                "Combinations[1].Athlete",
                "Combinations[1].Horse",
                "Competitions[0].Participations[0].Combination.Athlete",
                "Competitions[0].Participations[0].Combination.Horse",
            ],
            copies.Select(x => x.Path)
        );
        var inPool = copies[0];
        Assert.Null(inPool.ParticipationId);
        var inParticipation = copies[4];
        Assert.Equal("77", inParticipation.ParticipationId);
        Assert.Equal("Maria Georgieva", inParticipation.Name);
    }

    [Fact]
    public void The_other_half_of_a_Combination_is_the_partner_of_each_copy()
    {
        var setup = Setup(900, "Spring ride", [Combination(1, Person(11, "Ivan Petrov"), Person(21, "Burya"))]);

        var copies = IdentityCopies.Read("configure_events", setup);

        Assert.Equal("Burya", copies.Single(x => x.Kind == IdentityKind.Athlete).Partner);
        Assert.Equal("Ivan Petrov", copies.Single(x => x.Kind == IdentityKind.Horse).Partner);
    }

    [Fact]
    public void A_Participation_of_a_started_Event_holds_its_Combination_and_names_its_Event()
    {
        var participation = Participation(
            77,
            900,
            Combination(2, Person(12, "Maria Georgieva", "Maria Georgieva"), Person(22, "Vihar"))
        );

        var copies = IdentityCopies.Read("event_participations", participation);

        Assert.Equal(2, copies.Count);
        Assert.All(copies, x => Assert.Equal("900", x.EventId));
        Assert.All(copies, x => Assert.Equal("77", x.ParticipationId));
        Assert.All(copies, x => Assert.Equal("event_participations", x.Collection));
        Assert.Equal(["Combination.Athlete", "Combination.Horse"], copies.Select(x => x.Path));
    }

    [Fact]
    public void A_Ranking_of_before_holds_a_copy_of_every_Participation_and_the_copy_names_the_Participation()
    {
        var first = Participation(77, 900, Combination(2, Person(12, "Maria Georgieva"), Person(22, "Vihar")));
        var second = Participation(78, 900, Combination(3, Person(13, "Petar Dimitrov"), Person(23, "Zora")));
        var ranking = Ranking(300, 900, first, second);

        var copies = IdentityCopies.Read("event_rankings", ranking);

        Assert.Equal(4, copies.Count);
        Assert.Equal("Entries[1].Participation.Combination.Athlete", copies[2].Path);
        Assert.Equal("78", copies[2].ParticipationId);
        Assert.All(copies, x => Assert.Equal("900", x.EventId));
        Assert.All(copies, x => Assert.Equal("300", x.DocumentId));
    }

    [Fact]
    public void A_Handout_of_before_holds_a_copy_of_a_Participation()
    {
        var copy = Participation(77, 900, Combination(2, Person(12, "Maria Georgieva"), Person(22, "Vihar")));

        var copies = IdentityCopies.Read("event_handouts", Handout(400, 900, copy));

        Assert.Equal(
            ["Participation.Combination.Athlete", "Participation.Combination.Horse"],
            copies.Select(x => x.Path)
        );
        Assert.All(copies, x => Assert.Equal("77", x.ParticipationId));
    }

    [Fact]
    public void A_Ranking_that_names_its_Participations_by_id_holds_no_person()
    {
        Assert.Empty(IdentityCopies.Read("event_rankings", ReferencingRanking(300, 900, 77, 78)));
    }

    [Fact]
    public void An_EventId_that_is_null_names_no_Event()
    {
        var participation = Participation(
            77,
            BsonNull.Value,
            Combination(2, Person(12, "Maria Georgieva"), Person(22, "Vihar"))
        );

        var copies = IdentityCopies.Read("event_participations", participation);

        Assert.All(copies, x => Assert.Null(x.EventId));
    }

    [Fact]
    public void A_country_whose_two_codes_are_the_same_is_told_with_the_code_once_and_a_club_may_be_text()
    {
        var same = Person(1, "A", country: Country("Bulgaria", "BGR", "BGR"));
        var typedClub = Person(2, "B");
        typedClub["Club"] = " Sofia ";

        Assert.Equal("Bulgaria (BGR)", Assert.Single(IdentityCopies.Read("athletes", same)).Country);
        Assert.Equal("Sofia", Assert.Single(IdentityCopies.Read("athletes", typedClub)).Club);
    }

    [Fact]
    public void Shapes_that_the_data_should_not_have_are_read_without_failing()
    {
        var odd = new BsonDocument
        {
            ["_id"] = 1,
            ["EventId"] = 900,
            ["Combination"] = BsonNull.Value,
            ["Athlete"] = "Ivan Petrov", // a name and not a person
            ["Horse"] = new BsonArray { 1, 2 },
            ["Other"] = new BsonDocument
            {
                ["Athlete"] = new BsonDocument { ["_id"] = new BsonDocument(), ["Name"] = 5 },
            },
        };

        var copy = Assert.Single(IdentityCopies.Read("event_participations", odd));

        Assert.Equal("Other.Athlete", copy.Path);
        Assert.Null(copy.Name); // 5 is not a name
        Assert.Null(copy.LegacyId); // a document is not an id
    }

    [Fact]
    public void A_person_without_a_name_is_still_a_copy_so_that_the_report_can_say_where_it_is()
    {
        var copy = Assert.Single(IdentityCopies.Read("athletes", Person(5, null)));

        Assert.Null(copy.Name);
        Assert.Equal("5", copy.DocumentId);
    }

    [Fact]
    public void A_copy_tells_the_Guid_it_has_already_and_the_Tenant_of_its_document()
    {
        var guid = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var row = Person(new BsonBinaryData(guid, GuidRepresentation.Standard), "Ivan Petrov");
        row["TenantId"] = "country-bg";

        var written = Assert.Single(IdentityCopies.Read("athletes", row));
        var legacy = Assert.Single(IdentityCopies.Read("athletes", Person(5, "Ivan Petrov")));

        Assert.Equal(guid, written.CurrentId);
        Assert.Equal("country-bg", written.TenantId);
        Assert.Null(written.LegacyId); // a Guid is not a legacy id
        Assert.Null(legacy.CurrentId);
        Assert.Null(legacy.TenantId);
    }

    [Fact]
    public void A_copy_keeps_the_country_and_the_club_it_stores_as_they_are_stored()
    {
        var country = Country("Bulgaria", "BGR");
        var copy = Assert.Single(
            IdentityCopies.Read("athletes", Person(5, "Ivan Petrov", country: country, club: "Sofia"))
        );
        var bare = Assert.Single(IdentityCopies.Read("athletes", Person(6, "Ivan Petrov")));

        Assert.Equal(country, copy.CountryDocument);
        Assert.Equal("Sofia", copy.ClubDocument!["Name"].AsString);
        Assert.Null(bare.CountryDocument);
        Assert.Null(bare.ClubDocument);
    }
}
