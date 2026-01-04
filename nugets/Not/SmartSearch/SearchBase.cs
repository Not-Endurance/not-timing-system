namespace Not.SmartSearch;

public abstract class SearchBase<T>
{
    public abstract IEnumerable<T> GetMatches(IEnumerable<T> values, string search);
}
