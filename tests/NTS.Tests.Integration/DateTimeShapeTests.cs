using System.Reflection;
using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Identity;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0014: a time the database queries is a <c>DateTime</c> named <c>…Utc</c>, and every other time is a
/// <c>DateTimeOffset</c> that keeps its offset. A <c>DateTime</c> that does not say it is UTC, or a
/// <c>DateTimeOffset</c> stored as a plain date, fails here. The last two tests pin down the driver behaviour the ADR
/// rests on.
/// </summary>
public sealed class DateTimeShapeTests
{
    const BindingFlags EVERYTHING =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;
    static readonly Assembly[] OUR_ASSEMBLIES =
    [
        .. IdentifierShapeTests.OUR_ASSEMBLIES,
        typeof(NIdentityUser).Assembly, // Not.Identity
    ];

    [Fact]
    public void A_DateTime_is_named_Utc_and_is_read_back_as_UTC()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var properties = PropertiesOf(typeof(DateTime)).ToList();
        Assert.Contains(properties, x => x.Name == "ExpiresAtUtc"); // the scan reaches the documents of Not.Identity

        foreach (var property in properties)
        {
            if (!property.Name.EndsWith("Utc", StringComparison.Ordinal))
            {
                offenders.Add($"{Describe(property)} is not named ...Utc");
            }

            if (property.GetCustomAttribute<BsonDateTimeOptionsAttribute>()?.Kind != DateTimeKind.Utc)
            {
                offenders.Add($"{Describe(property)} lacks [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]");
            }
        }

        Assert.True(offenders.Count == 0, "DateTime properties that do not say they are UTC:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void A_DateTimeOffset_is_never_stored_as_a_plain_date()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var properties = PropertiesOf(typeof(DateTimeOffset)).ToList();
        Assert.Contains(properties, x => x.Name == "LockoutEnd"); // the scan reaches the documents of Not.Identity

        foreach (var property in properties)
        {
            if (property.GetCustomAttribute<BsonRepresentationAttribute>()?.Representation == BsonType.DateTime)
            {
                offenders.Add($"{Describe(property)} is stored as BsonType.DateTime and loses its offset");
            }
        }

        Assert.True(offenders.Count == 0, "DateTimeOffset properties stored without their offset:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void The_document_form_keeps_the_offset_and_the_ticks_and_carries_the_instant_as_a_date()
    {
        var original = new DateTimeOffset(2026, 10, 24, 15, 30, 45, 123, TimeSpan.FromHours(3));
        var precise = original.AddTicks(4567);
        var bson = new Sample
        {
            At = precise,
            Maybe = precise,
            AtUtc = original.UtcDateTime,
        }.ToBson();

        var raw = BsonSerializer.Deserialize<BsonDocument>(bson);
        var back = BsonSerializer.Deserialize<Sample>(bson);

        Assert.Equal(precise.Ticks, back.At.Ticks);
        Assert.Equal(precise.Offset, back.At.Offset);
        Assert.Equal(precise.Offset, back.Maybe!.Value.Offset);
        Assert.Equal(BsonType.DateTime, raw["At"].AsBsonDocument["DateTime"].BsonType);
        Assert.Equal(original.UtcDateTime, raw["At"].AsBsonDocument["DateTime"].ToUniversalTime());
        Assert.Equal(BsonType.DateTime, raw["AtUtc"].BsonType);
        Assert.Equal(original.UtcDateTime, back.AtUtc);
        Assert.Equal(DateTimeKind.Utc, back.AtUtc.Kind);
    }

    [Fact]
    public void A_plain_date_already_stored_is_read_into_the_document_form_as_UTC()
    {
        var instant = new DateTime(2026, 10, 24, 12, 30, 45, 123, DateTimeKind.Utc);
        var bson = new BsonDocument("At", new BsonDateTime(instant)).ToBson();

        var read = BsonSerializer.Deserialize<Sample>(bson);

        Assert.Equal(instant, read.At.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, read.At.Offset);
    }

    static IEnumerable<PropertyInfo> PropertiesOf(Type type)
    {
        return OUR_ASSEMBLIES
            .SelectMany(IdentifierShapeTests.TypesOf)
            .Where(x => !x.Name.Contains('<'))
            .SelectMany(x => x.GetProperties(EVERYTHING))
            .Where(x => (Nullable.GetUnderlyingType(x.PropertyType) ?? x.PropertyType) == type)
            .Where(x => x.GetCustomAttribute<CompilerGeneratedAttribute>() == null);
    }

    static string Describe(PropertyInfo property)
    {
        return $"{property.DeclaringType?.FullName}.{property.Name}";
    }

    sealed class Sample
    {
        [BsonRepresentation(BsonType.Document)]
        public DateTimeOffset At { get; set; }

        [BsonRepresentation(BsonType.Document)]
        public DateTimeOffset? Maybe { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime AtUtc { get; set; }
    }
}
