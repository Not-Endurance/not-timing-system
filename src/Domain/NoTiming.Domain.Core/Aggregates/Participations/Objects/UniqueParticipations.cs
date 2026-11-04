using System.Collections;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Aggregates.Participations.Objects;

public sealed class UniqueParticipations : IReadOnlyList<Participation>
{
    readonly List<Participation> _items = [];

    public UniqueParticipations() { }

    public UniqueParticipations(IEnumerable<Participation> participations)
    {
        AddRange(participations);
    }

    public Participation this[int index] => _items[index];

    public int Count => _items.Count;

    public void Add(Participation participation)
    {
        if (ContainsNumber(participation.Combination.Number))
        {
            return;
        }

        _items.Add(participation);
    }

    public void AddRange(IEnumerable<Participation> participations)
    {
        foreach (var participation in participations)
        {
            Add(participation);
        }
    }

    public void Upsert(Participation participation)
    {
        RemoveByNumber(participation.Combination.Number);
        Add(participation);
    }

    public bool Remove(Participation participation)
    {
        return RemoveByNumber(participation.Combination.Number);
    }

    public bool RemoveByNumber(int number)
    {
        var index = IndexOfNumber(number);
        if (index < 0)
        {
            return false;
        }

        _items.RemoveAt(index);
        return true;
    }

    public void Clear()
    {
        _items.Clear();
    }

    public bool ContainsNumber(int number)
    {
        return IndexOfNumber(number) >= 0;
    }

    public IEnumerator<Participation> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    int IndexOfNumber(int number)
    {
        return _items.FindIndex(x => x.Combination.Number == number);
    }
}
