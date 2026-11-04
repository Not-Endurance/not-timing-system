using Not.SmartSearch.Property;

namespace Not.SmartSearch.Builder;

public class SmartSearchBuilder<T>
{
    readonly List<SearchBase<T>> _searches = [];

    internal SmartSearchBuilder<T> Add(SearchBase<T> search)
    {
        _searches.Add(search);
        return this;
    }

    public SmartSearch<T> Build()
    {
        return new SmartSearch<T>(_searches);
    }
}

public static class SmartSearchBuilder
{
    public static SmartSearchBuilder<T> For<T>()
    {
        return new SmartSearchBuilder<T>();
    }

    public static SmartSearchBuilder<T> AddString<T>(this SmartSearchBuilder<T> builder, Func<T, string> valueSelector)
    {
        var stringSearch = new StringPropertySearch<T>(valueSelector);
        return builder.Add(stringSearch);
    }

    public static SmartSearchBuilder<T> AddInt<T>(this SmartSearchBuilder<T> builder, Func<T, int> valueSelector)
    {
        var intSearch = new IntPropertySearch<T>(valueSelector);
        return builder.Add(intSearch);
    }
}
