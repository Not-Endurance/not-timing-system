using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// A search of a collection by name (#638, ADR-0010, the rest-api skill), made by the database: every word of the text has to
/// be part of one of the names of a row, whatever the case and in any order, the names are in order or in the order the other
/// way round with the ids to tell the rows with the same name apart, and what is read is a page. A word is matched as it is
/// written and never as a pattern, and without regard to case by the database itself, which does it for any script: an
/// expression of the OData grammar could not, because MongoDB lowers the case of ASCII letters alone.
/// </summary>
internal sealed class NameSearch
{
    /// <summary>The fewest characters of the longest word of a search: less would be a list of the collection, which a search is not.</summary>
    public const int MIN_WORD_LENGTH = 3;

    /// <summary>Whether the words of a text are enough to look for: at least one of them is of <see cref="MIN_WORD_LENGTH"/> characters.</summary>
    public static bool HasLongWord(IReadOnlyList<string> words)
    {
        return words.Any(x => x.Length >= MIN_WORD_LENGTH);
    }

    public static NameSearch Of(JsonApiSearch search)
    {
        return new NameSearch(search.Words!, search.Descending, search.Skip, search.Take);
    }

    readonly IReadOnlyList<string> _words;
    readonly bool _descending;

    public NameSearch(IReadOnlyList<string> words, bool descending, int skip, int take)
    {
        _words = words;
        _descending = descending;
        Skip = skip;
        Take = take;
    }

    /// <summary>How many rows to leave out.</summary>
    public int Skip { get; }

    /// <summary>How many rows to read: one more than a page, to know whether another follows.</summary>
    public int Take { get; }

    /// <summary>The rows with, for every word, one of the fields that has it in it.</summary>
    public FilterDefinition<BsonDocument> Matching(params string[] fields)
    {
        return Builders<BsonDocument>.Filter.And(
            _words.Select(word =>
            {
                var pattern = new BsonRegularExpression(Regex.Escape(word), "i");
                return Builders<BsonDocument>.Filter.Or(
                    fields.Select(field => Builders<BsonDocument>.Filter.Regex(field, pattern))
                );
            })
        );
    }

    /// <summary>
    /// The order of the rows: by the field that names them, and by the fields that tell two rows with the same name apart (the
    /// id, where a row is one document), so that no row is twice or missing between two pages.
    /// </summary>
    public BsonDocument OrderBy(string name, params string[] thenBy)
    {
        var order = new BsonDocument { { name, _descending ? -1 : 1 } };
        foreach (var field in thenBy)
        {
            order.Add(field, 1);
        }

        return order;
    }

    /// <summary>A route has told a text that is too short before it asks for a read; a read that is asked for one anyway is refused here.</summary>
    public void EnsureEnough()
    {
        if (!HasLongWord(_words))
        {
            throw new ArgumentException($"A search is for a word of at least {MIN_WORD_LENGTH} characters.");
        }
    }
}
