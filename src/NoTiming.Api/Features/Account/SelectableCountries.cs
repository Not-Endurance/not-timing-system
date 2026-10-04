using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The countries a person can register from (#601): the ones in the <c>countries</c> collection that have a name and an
/// ISO code, because a Tenant is made from the ISO code (ADR-0002). A country without one is not offered and is
/// refused, and so is a row whose id is not a Guid. The documents are read as they are stored and not through the
/// models of the Functions API, which refuse a country without an ISO code altogether.
/// </summary>
internal sealed class SelectableCountries
{
    const string COLLECTION = "countries";

    readonly IMongoCollection<BsonDocument> _countries;

    public SelectableCountries(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _countries = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(COLLECTION);
    }

    public async Task<IReadOnlyList<Country>> AllAsync(CancellationToken cancellationToken)
    {
        var documents = await _countries.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        return
        [
            .. documents.Select(ToCountry).OfType<Country>().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<Country?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var filter = new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
        var document = await _countries.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document == null ? null : ToCountry(document);
    }

    static Country? ToCountry(BsonDocument document)
    {
        var name = TextOf(document, "Name");
        var isoCode = TextOf(document, "IsoCode");
        if (
            name == null
            || isoCode == null
            || !document.TryGetValue("_id", out var id)
            || id is not BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
        )
        {
            return null;
        }

        return new Country(
            binary.ToGuid(GuidRepresentation.Standard),
            name,
            isoCode,
            TextOf(document, "NfCode"),
            TextOf(document, "Locale")
        );
    }

    static string? TextOf(BsonDocument document, string field)
    {
        return
            document.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString.Trim()
            : null;
    }
}
