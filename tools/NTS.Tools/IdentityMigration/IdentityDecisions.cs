using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// What the owner has decided in the decisions file (#636): a decision per item, written under the key of the item, and a bulk rule
/// per class of item. An item's own decision beats the rule of its class; an item with neither is still to be decided. The file is
/// JSON: <c>{ "rules": { "NearMiss": "different" }, "items": { "athlete: a &lt;&gt; athlete: b": "same" } }</c>, and the dry run
/// writes it as a template with every item still to decide, which the owner edits and the next run reads.
/// </summary>
public sealed class IdentityDecisions
{
    static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // names are written as names, and the key of a pair has < and > in it
    };

    /// <summary>Reads the decisions file. A file that cannot be understood is refused whole, so that no decision is guessed at.</summary>
    /// <exception cref="FormatException">The text is not JSON, not an object, or has a rule or a decision it does not know.</exception>
    public static IdentityDecisions Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true });
        }
        catch (JsonException error)
        {
            throw new FormatException($"The decisions are not valid JSON: {error.Message}", error);
        }

        if (node is not JsonObject root)
        {
            throw new FormatException("The decisions are one JSON object, with \"rules\" and \"items\" in it.");
        }

        try
        {
            return Read(root);
        }
        catch (ArgumentException error)
        {
            // a name written twice: the JSON of the .NET that this is built for is read lazily, and says so on first use
            throw new FormatException($"The decisions write a name twice: {error.Message}", error);
        }
    }

    readonly JsonObject _root;
    readonly IReadOnlyDictionary<string, IdentityDecision> _items;
    readonly IReadOnlyDictionary<ReviewClass, IdentityDecision> _rules;

    IdentityDecisions(
        JsonObject root,
        IReadOnlyDictionary<string, IdentityDecision> items,
        IReadOnlyDictionary<ReviewClass, IdentityDecision> rules
    )
    {
        _root = root;
        _items = items;
        _rules = rules;
    }

    /// <summary>No decision at all: the first run, before the owner has seen the report.</summary>
    public static IdentityDecisions None { get; } =
        new(
            new JsonObject(),
            new Dictionary<string, IdentityDecision>(),
            new Dictionary<ReviewClass, IdentityDecision>()
        );

    /// <summary>The keys the file has a decision for.</summary>
    public IEnumerable<string> ItemKeys => _items.Keys;

    public IdentityDecision? Item(string key)
    {
        return _items.TryGetValue(key, out var decision) ? decision : null;
    }

    public IdentityDecision? Rule(ReviewClass match)
    {
        return _rules.TryGetValue(match, out var decision) ? decision : null;
    }

    /// <summary>
    /// The file to write: everything the owner wrote, kept as it was (and what this does not know of), with a rule for every class
    /// that has none yet and an item, to decide, for every key given that the file has no line for. The items are in order, so that
    /// the same data and decisions give the same file.
    /// </summary>
    public string ToJson(IEnumerable<string> toDecide)
    {
        var root = (JsonObject)_root.DeepClone();
        var rules = new JsonObject();
        foreach (var (name, value) in ObjectAt(root, "rules"))
        {
            rules.Add(name, value?.DeepClone());
        }

        foreach (var name in Enum.GetNames<ReviewClass>())
        {
            if (!rules.Any(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)))
            {
                rules.Add(name, null);
            }
        }

        var present = ObjectAt(root, "items")
            .ToDictionary(x => x.Key, x => x.Value?.DeepClone(), StringComparer.Ordinal);
        foreach (var key in toDecide.Where(x => !present.ContainsKey(x)))
        {
            present.Add(key, null);
        }

        var items = new JsonObject();
        foreach (var (key, value) in present.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            items.Add(key, value);
        }

        root["rules"] = rules;
        root["items"] = items;
        return root.ToJsonString(Written).Replace("\r\n", "\n") + "\n";
    }

    static IEnumerable<KeyValuePair<string, JsonNode?>> ObjectAt(JsonObject root, string name)
    {
        if (!root.TryGetPropertyValue(name, out var node) || node == null)
        {
            return [];
        }

        return node as JsonObject
            ?? throw new FormatException($"\"{name}\" is an object of decisions, written as {{ \"name\": \"same\" }}.");
    }

    static IdentityDecision? DecisionOf(string name, JsonNode? value)
    {
        if (value == null)
        {
            return null;
        }

        var text = value is JsonValue json && json.TryGetValue<string>(out var read) ? read.Trim() : null;
        return text switch
        {
            "" => null,
            _ when string.Equals(text, "same", StringComparison.OrdinalIgnoreCase) => IdentityDecision.Same,
            _ when string.Equals(text, "different", StringComparison.OrdinalIgnoreCase) => IdentityDecision.Different,
            _ => throw new FormatException(
                $"The decision for \"{name}\" is {value.ToJsonString()}: write same or different, or leave it empty."
            ),
        };
    }

    static IdentityDecisions Read(JsonObject root)
    {
        var rules = new Dictionary<ReviewClass, IdentityDecision>();
        foreach (var (name, value) in ObjectAt(root, "rules"))
        {
            if (!Enum.TryParse<ReviewClass>(name, ignoreCase: true, out var match) || !Enum.IsDefined(match))
            {
                throw new FormatException(
                    $"The rule \"{name}\" is not a class of item: use {string.Join(", ", Enum.GetNames<ReviewClass>())}."
                );
            }

            if (DecisionOf(name, value) is { } decision && !rules.TryAdd(match, decision))
            {
                throw new FormatException(
                    $"The rule \"{name}\" is given twice, in two ways of writing the same class."
                );
            }
        }

        var items = new Dictionary<string, IdentityDecision>(StringComparer.Ordinal);
        foreach (var (key, value) in ObjectAt(root, "items"))
        {
            if (DecisionOf(key, value) is { } decision)
            {
                items[key] = decision;
            }
        }

        return new IdentityDecisions(root, items, rules);
    }
}
