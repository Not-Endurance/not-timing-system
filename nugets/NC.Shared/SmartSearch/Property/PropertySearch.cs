namespace Not.SmartSearch.Property;

internal abstract class PropertySearch<T, TValue> : SearchBase<T>
{
    readonly Func<T, TValue?> _selector;

    protected PropertySearch(Func<T, TValue?> selector)
    {
        _selector = selector;
    }

    protected abstract TValue ConvertTerm(string term);

    protected virtual bool IsMatch(TValue? selected, TValue search)
    {
        return EqualityComparer<TValue?>.Default.Equals(selected, search);
    }

    public override IEnumerable<T> GetMatches(IEnumerable<T> instances, string value)
    {
        // Try-catch because it is not possible to define the return type of ConvertSearch as nullable
        // and use null to check if the term is valid for the given search
        // see more: https://stackoverflow.com/questions/71716403/override-abstract-method-as-nullable-both-reference-and-value-type
        try
        {
            var result = new List<T>();
            var term = ConvertTerm(value);
            foreach (var instance in instances)
            {
                var selected = _selector(instance);
                if (IsMatch(selected, term))
                {
                    result.Add(instance);
                }
            }
            return result;
        }
        catch (InvalidSearchTermException)
        {
            return [];
        }
    }
}
