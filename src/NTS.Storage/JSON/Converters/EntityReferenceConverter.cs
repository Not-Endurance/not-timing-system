using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Not.Domain.Aggregates;
using Not.Serialization.JSON;

namespace NTS.Storage.JSON.Converters;

public class EntityReferenceConverter<T> : JsonConverterBase
    where T : Aggregate
{
    const string DOMAIN_REF_PROPERTY = "$domainRef";
    static readonly string DOMAIN_ID_PROPERTY = nameof(Aggregate.Id);

    readonly object _lock = new();
    readonly Type _type = typeof(T);
    Dictionary<int, T> _instancesById = [];
    bool _canWrite = true;
    bool _canRead = true;

    public override bool CanWrite => _canWrite;
    public override bool CanRead => _canRead;

    public override bool CanConvert(Type objectType)
    {
        return _type.IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var entity =
            value as T
            ?? throw new JsonSerializationException($"Unexpected value when converting: {value?.GetType().Name}");

        if (_instancesById.ContainsKey(entity.Id))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(DOMAIN_REF_PROPERTY);
            writer.WriteValue(entity.Id);
            writer.WriteEndObject();
        }
        else
        {
            // JsonConverters as singletons, which means in a high performance scenario it can be used by multiple threads
            lock (_lock)
            {
                _instancesById[entity.Id] = entity;
                // Disable CanWrite before Serialize otherwise it ends up calling WriteJson recursively
                _canWrite = false;
                serializer.Serialize(writer, entity);
                _canWrite = true;
            }
        }
    }

    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        lock (_lock)
        {
            _canRead = false;
            var jObject = JObject.Load(reader);
            if (jObject.ContainsKey(DOMAIN_REF_PROPERTY))
            {
                _canRead = true;
                var id = GetIdValue(jObject, DOMAIN_REF_PROPERTY);
                if (!_instancesById.TryGetValue(id, out var existingEntity))
                {
                    throw new JsonSerializationException($"Unresolved domain reference value '{id}'");
                }
                return existingEntity;
            }

            if (jObject.ContainsKey(DOMAIN_ID_PROPERTY))
            {
                var id = GetIdValue(jObject, DOMAIN_ID_PROPERTY);
                var entity = jObject.ToObject<T>(serializer)!;
                _canRead = true;
                _instancesById[id] = entity;
                return entity;
            }
        }

        throw new JsonSerializationException("Expected Id or DomainId in serialized JSON.");
    }

    // Has to reset after each call to serialize / deserialize completes, because otherwise
    // the dictionary will be full with all instances and will always serialize every object as
    // a domain reference, which in turn will result in failure during deserialization after app restart
    public override void Reset()
    {
        _instancesById = [];
    }

    int GetIdValue(JObject jObject, string jsonProperty)
    {
        var result = jObject[jsonProperty];
        if (result == null)
        {
            throw new JsonSerializationException($"Value for '{jsonProperty}' cannot be null");
        }
        return result.Value<int>();
    }
}
