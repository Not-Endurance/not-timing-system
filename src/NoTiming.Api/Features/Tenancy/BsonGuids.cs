using MongoDB.Bson;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// Guids as the documents of the Functions API hold them: binary of the standard subtype (ADR-0009). Reading one that is
/// not of that shape finds none, so a document that is not as it should be is not mistaken for one that is.
/// </summary>
internal static class BsonGuids
{
    public static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    public static Guid? Of(BsonDocument document, string field)
    {
        return
            document.TryGetValue(field, out var value)
            && value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            ? binary.ToGuid(GuidRepresentation.Standard)
            : null;
    }
}
