namespace NTS.Domain.Setup.Services.Identities;

/// <summary>Which of a number of copies are joined, as a union of sets that is found by the first copy of the set.</summary>
internal sealed class Joins
{
    readonly int[] _parent;

    public Joins(int count)
    {
        _parent = [.. Enumerable.Range(0, count)];
    }

    public int RootOf(int copy)
    {
        while (_parent[copy] != copy)
        {
            _parent[copy] = _parent[_parent[copy]];
            copy = _parent[copy];
        }

        return copy;
    }

    public void Join(int one, int another)
    {
        var first = RootOf(one);
        var second = RootOf(another);
        if (first != second)
        {
            _parent[Math.Max(first, second)] = Math.Min(first, second);
        }
    }
}
