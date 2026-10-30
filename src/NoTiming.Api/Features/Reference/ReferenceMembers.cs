using System.Reflection;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// The members of a model of reference data as the resource shows them (#603, the rest-api skill): the attributes of
/// the document are its members in camelCase, without the id, which is the resource's own. A member the server owns,
/// such as the Tenant or the Main Operator, is shown and cannot be written, and one the resource keeps to itself, such as
/// the account an Athlete is linked to, is neither shown nor written, so that a document read can be sent back as it is
/// after the id and what the server owns are taken out. A document that names a member the resource has not is refused,
/// whatever it says, so nothing it names is ignored without telling.
/// </summary>
internal sealed class ReferenceMembers<TModel>
    where TModel : class, new()
{
    static readonly string[] SERVER_OWNED = ["Id", "TenantId"];

    readonly Dictionary<string, PropertyInfo> _writable = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _hidden;

    public ReferenceMembers(IEnumerable<string> hidden, IEnumerable<string> readOnly)
    {
        _hidden = [.. hidden];
        var notWritable = SERVER_OWNED.Concat(readOnly).Concat(_hidden).ToHashSet();
        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod?.IsPublic == true && !notWritable.Contains(property.Name))
            {
                _writable[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property;
            }
        }
    }

    /// <summary>The members the resource keeps to itself.</summary>
    public IReadOnlyCollection<string> Hidden => _hidden;

    /// <summary>The attributes of the model: every member it has that is not the id and not kept to the resource.</summary>
    public Dictionary<string, JsonElement> AttributesOf(TModel model)
    {
        var element = JsonSerializer.SerializeToElement(model, JsonApiResults.Options);
        var hidden = _hidden.Select(JsonNamingPolicy.CamelCase.ConvertName).ToHashSet();
        var attributes = new Dictionary<string, JsonElement>();
        foreach (var member in element.EnumerateObject())
        {
            if (member.Name != "id" && !hidden.Contains(member.Name))
            {
                attributes[member.Name] = member.Value;
            }
        }

        return attributes;
    }

    /// <summary>
    /// Puts the members a document names into the model, each as the type of the member it is. A refusal is returned when
    /// the document names a member that cannot be written or gives one a value of another type, and then nothing of the
    /// model is to be used.
    /// </summary>
    public IResult? Apply(TModel model, IReadOnlyDictionary<string, JsonElement> members, List<PropertyInfo> named)
    {
        var unknown = members.Keys.Where(x => !_writable.ContainsKey(x)).ToList();
        if (unknown.Count > 0)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "The document names a member that is not one of the resource's, or one that the server alone sets.",
                string.Join(", ", unknown)
            );
        }

        foreach (var (name, value) in members)
        {
            var property = _writable[name];
            try
            {
                property.SetValue(model, value.Deserialize(property.PropertyType, JsonApiResults.Options));
            }
            catch (JsonException)
            {
                return JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "malformed-request",
                    "The request is malformed.",
                    $"The member '{name}' is not of the type it has."
                );
            }

            named.Add(property);
        }

        return null;
    }

    /// <summary>
    /// What changes a stored row into the model in the named members and in no other: the members are set to what the
    /// database stores for them, and one that the database does not store, because it is null or the default, is removed.
    /// </summary>
    public BsonDocument UpdateOf(TModel model, IEnumerable<PropertyInfo> named)
    {
        var stored = model.ToBsonDocument();
        var classMap = BsonClassMap.LookupClassMap(typeof(TModel));
        var set = new BsonDocument();
        var unset = new BsonDocument();
        foreach (var property in named)
        {
            var element = classMap.GetMemberMap(property.Name).ElementName;
            if (stored.TryGetValue(element, out var value))
            {
                set[element] = value;
            }
            else
            {
                unset[element] = 1;
            }
        }

        var update = new BsonDocument();
        if (set.ElementCount > 0)
        {
            update["$set"] = set;
        }

        if (unset.ElementCount > 0)
        {
            update["$unset"] = unset;
        }

        return update;
    }
}
