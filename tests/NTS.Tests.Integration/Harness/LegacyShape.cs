using MongoDB.Bson;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// Turns a document made by the code of today into the document of before the Guids (#637): every Guid that is an id or a reference
/// becomes an integer, the same Guid the same integer, so that the references still meet. The time events of a Phase were made as
/// Guids by an earlier migration and stay what they are.
/// </summary>
internal sealed class LegacyShape
{
    readonly Dictionary<Guid, int> _numbers = [];
    int _next = 1000;

    /// <summary>The integer a Guid became, made when it is asked for.</summary>
    public int NumberOf(Guid id)
    {
        if (!_numbers.TryGetValue(id, out var number))
        {
            number = _next++;
            _numbers[id] = number;
        }

        return number;
    }

    /// <summary>Gives a Guid an integer of its own choosing, such as the id an Event had.</summary>
    public void Name(Guid id, int number)
    {
        _numbers[id] = number;
    }

    public BsonDocument Downgrade(BsonDocument document)
    {
        var downgraded = new BsonDocument();
        foreach (var element in document.Elements)
        {
            downgraded[element.Name] = element.Name == "Events" ? element.Value.DeepClone() : Downgrade(element.Value);
        }

        return downgraded;
    }

    BsonValue Downgrade(BsonValue value)
    {
        return value switch
        {
            BsonDocument document => Downgrade(document),
            BsonArray array => new BsonArray(array.Select(Downgrade)),
            BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary => new BsonInt32(
                NumberOf(binary.ToGuid(GuidRepresentation.Standard))
            ),
            _ => value.DeepClone(),
        };
    }
}
