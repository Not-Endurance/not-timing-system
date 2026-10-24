using MongoDB.Bson;

namespace NTS.Tools.ParticipationCopies;

/// <summary>
/// How a copy of a Participation, embedded in a Ranking entry or a Handout, differs from the Participation that is
/// stored. The derived values do not count: they follow from the times, and the migration removes them. A field that
/// is not there, is null, is false or is zero is the same thing, as the application does not write a default.
/// </summary>
internal static class CopyDifferences
{
    public static IReadOnlyList<string> Between(BsonDocument copy, BsonDocument stored)
    {
        var differences = new List<string>();
        CompareDocuments("", DerivedValues.Without(copy), DerivedValues.Without(stored), differences);
        return differences;
    }

    static void Compare(string path, BsonValue? copy, BsonValue? stored, List<string> differences)
    {
        if (IsEmpty(copy) && IsEmpty(stored))
        {
            return;
        }

        if (copy is BsonDocument copyDocument && stored is BsonDocument storedDocument)
        {
            CompareDocuments(path, copyDocument, storedDocument, differences);
        }
        else if (copy is BsonArray copyArray && stored is BsonArray storedArray)
        {
            CompareArrays(path, copyArray, storedArray, differences);
        }
        else if (copy == null || stored == null || !AreEqual(copy, stored))
        {
            differences.Add(path);
        }
    }

    static void CompareDocuments(string path, BsonDocument copy, BsonDocument stored, List<string> differences)
    {
        foreach (var name in copy.Names.Union(stored.Names).Order(StringComparer.Ordinal))
        {
            var fieldPath = path.Length == 0 ? name : $"{path}.{name}";
            Compare(fieldPath, ValueOf(copy, name), ValueOf(stored, name), differences);
        }
    }

    static void CompareArrays(string path, BsonArray copy, BsonArray stored, List<string> differences)
    {
        if (copy.Count != stored.Count)
        {
            differences.Add(path);
        }

        for (var index = 0; index < Math.Min(copy.Count, stored.Count); index++)
        {
            Compare($"{path}[{index}]", copy[index], stored[index], differences);
        }
    }

    static BsonValue? ValueOf(BsonDocument document, string name)
    {
        return document.TryGetValue(name, out var value) ? value : null;
    }

    static bool IsEmpty(BsonValue? value)
    {
        return value == null
            || value.IsBsonNull
            || (value.IsBoolean && !value.AsBoolean)
            || (value.IsNumeric && value.ToDouble() == 0);
    }

    static bool AreEqual(BsonValue copy, BsonValue stored)
    {
        return copy.IsNumeric && stored.IsNumeric ? copy.ToDouble() == stored.ToDouble() : copy.Equals(stored);
    }
}
