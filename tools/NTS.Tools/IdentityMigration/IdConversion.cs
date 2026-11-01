using MongoDB.Bson;
using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>The Guids that the identities of the Athletes and of the Horses have, by the spelling a copy is written as (#637).</summary>
public interface IIdentityGuids
{
    /// <returns>The Guid of the identity the spelling is of, none if it is of no identity.</returns>
    Guid? GuidOf(IdentityKind kind, string spellingKey);
}

/// <summary>An id that the table cannot convert, and where it is (#637).</summary>
public sealed class IdProblem
{
    public IdProblem(string collection, string documentId, string path, string message)
    {
        Collection = collection;
        DocumentId = documentId;
        Path = path;
        Message = message;
    }

    public string Collection { get; }

    /// <summary>The id of the document, as the reports tell it.</summary>
    public string DocumentId { get; }

    /// <summary>The place of the id in the document: <c>Phases[0].Events[0]._id</c>.</summary>
    public string Path { get; }

    public string Message { get; }
}

/// <summary>An id that refers to a document of another collection (#637).</summary>
public sealed class IdReference
{
    public IdReference(string target, Guid id, string path)
    {
        Target = target;
        Id = id;
        Path = path;
    }

    /// <summary>The collection the id is of a document in.</summary>
    public string Target { get; }

    public Guid Id { get; }
    public string Path { get; }
}

/// <summary>The Guid an Athlete or a Horse has in a document (#637), once it is converted.</summary>
public sealed class IdPerson
{
    public IdPerson(IdentityKind kind, Guid id, string path)
    {
        Kind = kind;
        Id = id;
        Path = path;
    }

    public IdentityKind Kind { get; }
    public Guid Id { get; }
    public string Path { get; }
}

/// <summary>A document after its ids are converted, and what was found in it (#637).</summary>
public sealed class ConvertedDocument
{
    internal ConvertedDocument(
        BsonDocument document,
        bool changed,
        Guid? id,
        List<IdProblem> problems,
        List<IdReference> references,
        List<IdPerson> people
    )
    {
        Document = document;
        Changed = changed;
        Id = id;
        Problems = problems;
        References = references;
        People = people;
    }

    public BsonDocument Document { get; }

    /// <summary>Whether any id was converted.</summary>
    public bool Changed { get; }

    /// <summary>The Guid the document is stored under once it is converted, none if its collection keeps another kind of id.</summary>
    public Guid? Id { get; }

    /// <summary>What the table cannot convert; a document with problems is not written.</summary>
    public IReadOnlyList<IdProblem> Problems { get; }

    /// <summary>The ids of the document that refer to a document of another collection.</summary>
    public IReadOnlyList<IdReference> References { get; }

    /// <summary>The Guids of the Athletes and the Horses the document holds.</summary>
    public IReadOnlyList<IdPerson> People { get; }
}

/// <summary>
/// Converts the ids of a document by the table (#637, ADR-0009): an integer in an id path becomes the Guid of its space and its scope,
/// an Athlete or a Horse takes the Guid of its identity, a Guid is left as it is, and an id the table cannot convert is told and not
/// guessed. It is a pure function of the document and the identities: a document that is converted already comes back as it is, so
/// that a run on converted data changes nothing and a run that stopped finishes when it is run again.
/// </summary>
public static class IdConversion
{
    const string ID = "_id";
    const string EVENT_ID = "EventId";

    /// <param name="collection">The collection the document is of.</param>
    /// <param name="document">The document, which is not changed.</param>
    /// <param name="identities">The Guids of the identities; none when only what is converted already is looked at, and then an Athlete or a Horse that is an integer is a problem.</param>
    public static ConvertedDocument Convert(string collection, BsonDocument document, IIdentityGuids? identities)
    {
        return new Conversion(collection, document, identities).Run();
    }

    sealed class Conversion
    {
        readonly string _collection;
        readonly BsonDocument _source;
        readonly IIdentityGuids? _identities;
        readonly BsonDocument _converted;
        readonly string _documentId;
        readonly Dictionary<string, IdentityCopy> _copies;
        readonly HashSet<string> _handled = [];
        readonly List<IdProblem> _problems = [];
        readonly List<IdReference> _references = [];
        readonly List<IdPerson> _people = [];
        Guid? _event;
        Guid? _own;

        public Conversion(string collection, BsonDocument document, IIdentityGuids? identities)
        {
            _collection = collection;
            _source = document;
            _identities = identities;
            _converted = document.DeepClone().AsBsonDocument;
            _documentId = document.TryGetValue(ID, out var id) ? IdentityCopies.Describe(id) : "";
            _copies = IdentityCopies.Read(collection, document).ToDictionary(x => x.Path);
        }

        public ConvertedDocument Run()
        {
            _event = EventOf();
            _own = OwnOf();
            Walk(_converted, "", "");
            foreach (var copy in _copies.Values.Where(x => !_handled.Contains(x.Path)))
            {
                var place = copy.Path.Length == 0 ? ID : $"{copy.Path}.{ID}";
                _problems.Add(new IdProblem(_collection, _documentId, place, "The table has no id for this person."));
            }

            return new ConvertedDocument(
                _converted,
                !_converted.Equals(_source),
                _own,
                _problems,
                _references,
                _people
            );
        }

        /// <summary>The Event a document names. A Setup and an Event are the Event themselves, and nothing in them is scoped to another.</summary>
        Guid? EventOf()
        {
            return _source.TryGetValue(EVENT_ID, out var value) ? GuidOf(value, IdPaths.EVENT, null) : null;
        }

        /// <summary>The Guid the document is stored under, from its own id.</summary>
        Guid? OwnOf()
        {
            var path = IdPaths.Find(_collection, ID) != null ? ID : "Id";
            if (IdPaths.Find(_collection, path) is not { } row || !_source.TryGetValue(path, out var value))
            {
                return null;
            }

            if (row.Role == IdRole.Identity)
            {
                return _identities is { } identities && _copies.TryGetValue("", out var copy)
                    ? identities.GuidOf(copy.Kind, IdentityKeys.SpellingOf(copy))
                    : GuidOf(value, row.Space, null);
            }

            return GuidOf(value, row.Space, ScopeOf(row.Scope));
        }

        void Walk(BsonDocument node, string shape, string place)
        {
            foreach (var element in node.Elements.ToList())
            {
                var name = element.Name;
                var path = shape.Length == 0 ? name : $"{shape}.{name}";
                var at = place.Length == 0 ? name : $"{place}.{name}";
                if (IdPaths.IsRetired(_collection, path))
                {
                    continue;
                }

                if (IdPaths.Find(_collection, path) is { } row)
                {
                    node[name] = ConvertId(row, element.Value, at, place);
                }
                else if (IsIdName(name) && IsInteger(element.Value))
                {
                    Problem(at, $"An integer id the table does not know: {element.Value}.");
                }
                else if (element.Value is BsonDocument inner)
                {
                    Walk(inner, path, at);
                }
                else if (element.Value is BsonArray array)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        if (array[i] is BsonDocument item)
                        {
                            Walk(item, path, $"{at}[{i}]");
                        }
                    }
                }
            }
        }

        BsonValue ConvertId(IdPath row, BsonValue value, string at, string person)
        {
            switch (row.Role)
            {
                case IdRole.Born:
                    if (IsInteger(value))
                    {
                        Problem(at, $"An integer where a Guid was made: {value}.");
                    }

                    return value;
                case IdRole.Identity:
                    return ConvertPerson(row, value, at, person);
                default:
                    return ConvertPlain(row, value, at);
            }
        }

        BsonValue ConvertPlain(IdPath row, BsonValue value, string at)
        {
            if (value.IsBsonNull)
            {
                return value;
            }

            if (Existing(value) is { } existing)
            {
                Refer(row, existing, at);
                return value;
            }

            if (!IsInteger(value))
            {
                Problem(at, $"An id that is neither an integer nor a Guid: {value}.");
                return value;
            }

            var scope = ScopeOf(row.Scope);
            if (row.Scope != IdScope.None && scope == null)
            {
                Problem(at, $"An id whose {row.Scope} cannot be told: {value}.");
                return value;
            }

            var guid = LegacyGuids.Of(row.Space, scope, IntegerOf(value));
            Refer(row, guid, at);
            return Uuid(guid);
        }

        BsonValue ConvertPerson(IdPath row, BsonValue value, string at, string person)
        {
            var kind = row.Space == IdPaths.ATHLETE ? IdentityKind.Athlete : IdentityKind.Horse;
            _handled.Add(person);
            if (Existing(value) is { } existing)
            {
                // it is the Guid it was given, by a run of this or by the code that made it, and it stays: the spelling of a copy that is
                // converted no longer says what it said, so the identities cannot be asked what it should be
                _people.Add(new IdPerson(kind, existing, person));
                return value;
            }

            Guid? assigned = null;
            if (_identities != null && _copies.TryGetValue(person, out var copy))
            {
                assigned = _identities.GuidOf(kind, IdentityKeys.SpellingOf(copy));
            }

            if (assigned == null)
            {
                Problem(at, _identities == null ? $"An integer remains: {value}." : "This person is of no identity.");
                return value;
            }

            _people.Add(new IdPerson(kind, assigned.Value, person));
            return Uuid(assigned.Value);
        }

        Guid? ScopeOf(IdScope scope)
        {
            return scope switch
            {
                IdScope.Event => _event,
                IdScope.Document => _own,
                _ => null,
            };
        }

        /// <summary>The Guid an id value is: the Guid it is, or the one an integer becomes; none for anything else.</summary>
        Guid? GuidOf(BsonValue value, string space, Guid? scope)
        {
            if (Existing(value) is { } existing)
            {
                return existing;
            }

            return IsInteger(value) ? LegacyGuids.Of(space, scope, IntegerOf(value)) : null;
        }

        void Refer(IdPath row, Guid id, string at)
        {
            if (row.Target != null)
            {
                _references.Add(new IdReference(row.Target, id, at));
            }
        }

        void Problem(string at, string message)
        {
            _problems.Add(new IdProblem(_collection, _documentId, at, message));
        }

        static Guid? Existing(BsonValue value)
        {
            return value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
                ? binary.ToGuid(GuidRepresentation.Standard)
                : null;
        }

        static bool IsIdName(string name)
        {
            return name is ID or "Id" || name.EndsWith("Id", StringComparison.Ordinal);
        }

        static bool IsInteger(BsonValue value)
        {
            return value switch
            {
                BsonInt32 or BsonInt64 => true,
                BsonDouble number => number.Value % 1 == 0,
                _ => false,
            };
        }

        static long IntegerOf(BsonValue value)
        {
            return value.ToInt64();
        }

        static BsonBinaryData Uuid(Guid id)
        {
            return new BsonBinaryData(id, GuidRepresentation.Standard);
        }
    }
}
