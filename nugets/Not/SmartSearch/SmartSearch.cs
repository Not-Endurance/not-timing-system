namespace Not.SmartSearch;

public class SmartSearch<T>
{
    readonly List<SearchBase<T>> _searches = [];

    public SmartSearch(IEnumerable<SearchBase<T>> searches)
    {
        _searches = searches.ToList();
    }

    public IEnumerable<T> Find(IEnumerable<T> values, string term)
    {
        var results = new List<T>();
        foreach (var search in _searches)
        {
            foreach (var match in search.GetMatches(values, term))
            {
                if (!results.Contains(match))
                {
                    results.Add(match);
                }
            }
        }
        return results;
    }
}
