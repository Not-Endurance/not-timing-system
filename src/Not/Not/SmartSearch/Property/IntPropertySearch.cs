namespace Not.SmartSearch.Property;

internal class IntPropertySearch<T> : PropertySearch<T, int>
{
    public IntPropertySearch(Func<T, int> selector)
        : base(selector) { }

    protected override int ConvertTerm(string value)
    {
        if (!int.TryParse(value, out var search))
        {
            throw new InvalidSearchTermException($"Search term '{value}' cannot be parsed to a valid integer");
        }
        return search;
    }
}
