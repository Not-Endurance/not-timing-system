using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;
using NTS.Contracts.Shared;
using NTS.Tools.IdentityMigration;
using CoreModels = NTS.Contracts.Core.Models;
using SetupModels = NTS.Contracts.Setup.Models;
using SharedModels = NTS.Contracts.Shared.Models;
using WatcherModels = NTS.Contracts.Watcher.Models;

namespace NTS.Tests.Unit.Temporary;

/// <summary>
/// #637, ADR-0009: the table that maps every id of every collection to its id space and scope covers every id property of the
/// models, so that a model that gains an id, or a new collection, fails here and not in a cutover. The paths are found in the
/// types, not typed in.
/// </summary>
public sealed class IdPathsTests
{
    static readonly (string Collection, Type Model)[] COLLECTIONS =
    [
        ("athletes", typeof(SetupModels.AthleteModel)),
        ("horses", typeof(SetupModels.HorseModel)),
        ("clubs", typeof(SetupModels.ClubModel)),
        ("countries", typeof(SharedModels.CountryModel)),
        ("users", typeof(SetupModels.UserModel)),
        ("configure_events", typeof(SetupModels.ConfigureEventModel)),
        ("event_informations", typeof(CoreModels.EventInformationModel)),
        ("event_officials", typeof(CoreModels.OfficialModel)),
        ("event_operators", typeof(CoreModels.OperatorModel)),
        ("event_participations", typeof(CoreModels.ParticipationModel)),
        ("event_rankings", typeof(CoreModels.RankingModel)),
        ("event_handouts", typeof(CoreModels.HandoutModel)),
        ("event_user_sessions", typeof(WatcherModels.NtsUserSessionModel)),
    ];

    public static TheoryData<string> Collections()
    {
        var data = new TheoryData<string>();
        foreach (var (collection, _) in COLLECTIONS)
        {
            data.Add(collection);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void The_table_has_every_id_property_of_the_model_of_the_collection_and_nothing_else(string collection)
    {
        var model = COLLECTIONS.Single(x => x.Collection == collection).Model;

        var expected = IdPropertiesOf(model, "", []).Order(StringComparer.Ordinal).ToList();
        var tabled = IdPaths
            .All.Where(x => x.Collection == collection)
            .Select(x => x.Path)
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, tabled);
    }

    [Fact]
    public void Every_document_model_is_a_collection_of_the_table_or_one_the_cutover_leaves()
    {
        var documents = typeof(IDocument)
            .Assembly.GetTypes()
            .Where(x => x is { IsClass: true, IsAbstract: false } && typeof(IDocument).IsAssignableFrom(x))
            .ToList();

        var unknown = documents.Except(COLLECTIONS.Select(x => x.Model)).Select(x => x.FullName);

        Assert.Empty(unknown);
    }

    [Fact]
    public void Every_collection_of_the_table_is_a_model_collection_above()
    {
        Assert.Equal(
            COLLECTIONS.Select(x => x.Collection).Order(StringComparer.Ordinal),
            IdPaths.Collections.Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void A_path_is_in_the_table_once()
    {
        var duplicated = IdPaths
            .All.GroupBy(x => (x.Collection, x.Path))
            .Where(x => x.Count() > 1)
            .Select(x => $"{x.Key.Collection} {x.Key.Path}");

        Assert.Empty(duplicated);
    }

    [Fact]
    public void An_id_that_is_scoped_has_the_scope_in_its_document()
    {
        // an Event is told by the document itself (a configure event, an Event) or by the Event it names
        var withoutAnEvent = IdPaths
            .All.Where(x => x.Scope == IdScope.Event && x.Collection is "configure_events" or "event_informations")
            .Select(x => $"{x.Collection} {x.Path}");

        Assert.Empty(withoutAnEvent);
    }

    /// <summary>The ids of a model as the paths of the elements that store them: <c>Competitions.Participations.Combination.Athlete._id</c>.</summary>
    static IEnumerable<string> IdPropertiesOf(Type model, string prefix, HashSet<Type> seen)
    {
        if (!seen.Add(model))
        {
            yield break;
        }

        var properties = model
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<BsonIgnoreAttribute>() == null && x.GetIndexParameters().Length == 0)
            .ToList();
        var hasAnotherId = properties.Any(x => x.GetCustomAttribute<BsonIdAttribute>() != null);
        foreach (var property in properties)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var isId =
                property.GetCustomAttribute<BsonIdAttribute>() != null || (property.Name == "Id" && !hasAnotherId);
            var name = isId ? "_id" : property.Name;
            if (type == typeof(Guid) || type == typeof(int))
            {
                if (isId || property.Name.EndsWith("Id", StringComparison.Ordinal))
                {
                    yield return prefix + name;
                }

                continue;
            }

            var element = type.IsArray ? type.GetElementType()! : type;
            if (element.Namespace?.StartsWith("NTS.Contracts", StringComparison.Ordinal) != true || element.IsEnum)
            {
                continue;
            }

            foreach (var inner in IdPropertiesOf(element, prefix + name + ".", [.. seen]))
            {
                yield return inner;
            }
        }
    }
}
