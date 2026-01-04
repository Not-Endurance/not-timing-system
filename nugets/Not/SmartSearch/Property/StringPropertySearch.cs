using System;

namespace Not.SmartSearch.Property;

internal class StringPropertySearch<T> : PropertySearch<T, string>
{
    public StringPropertySearch(Func<T, string> selector)
        : base(selector) { }

    protected override string ConvertTerm(string value)
    {
        return value;
    }

    protected override bool IsMatch(string? selected, string term)
    {
        return selected?.Contains(term) ?? false;
    }
}
