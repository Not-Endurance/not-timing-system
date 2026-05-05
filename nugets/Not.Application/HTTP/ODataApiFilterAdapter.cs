using System.Globalization;
using System.Linq.Expressions;

namespace Not.Application.HTTP;

public static class ODataApiFilterAdapter
{
    const string FILTER_QUERY = "$filter";

    public static IReadOnlyDictionary<string, string> ParseFilters<T>(
        IEnumerable<Expression<Func<T, bool>>> filters
    )
    {
        if (TryParseFilters(filters, out var queryParameters))
        {
            return queryParameters;
        }

        throw new NotSupportedException("Unable to parse Filter expressions into OData query filter.");
    }

    public static bool TryParseFilters<T>(
        IEnumerable<Expression<Func<T, bool>>> filters,
        out IReadOnlyDictionary<string, string> queryParameters
    )
    {
        var oDataFilters = new List<string>();
        foreach (var filter in filters)
        {
            if (!TryCreateFilter(filter, out var oDataFilter))
            {
                queryParameters = new Dictionary<string, string>();
                return false;
            }

            oDataFilters.Add(oDataFilter);
        }

        var combinedFilter = CombineFilterValues(oDataFilters);
        if (string.IsNullOrWhiteSpace(combinedFilter))
        {
            queryParameters = new Dictionary<string, string>();
            return true;
        }

        queryParameters = new Dictionary<string, string> { [FILTER_QUERY] = combinedFilter };
        return true;
    }

    static bool TryCreateFilter<T>(
        Expression<Func<T, bool>> filter,
        out string oDataFilter
    )
    {
        if (TryCreateFilter(filter.Body, out oDataFilter))
        {
            return true;
        }

        oDataFilter = "";
        return false;
    }

    static string CombineFilterValues(IEnumerable<string?> filters)
    {
        var values = filters
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();
        return values.Length == 1 ? values[0] : string.Join(" and ", values.Select(x => $"({x})"));
    }

    static bool TryCreateFilter(Expression expression, out string oDataFilter)
    {
        expression = StripConversion(expression);
        if (
            expression is BinaryExpression
            {
                NodeType: ExpressionType.AndAlso or ExpressionType.And
            } binary
        )
        {
            if (
                TryCreateFilter(binary.Left, out var left)
                && TryCreateFilter(binary.Right, out var right)
            )
            {
                oDataFilter = $"{left} and {right}";
                return true;
            }

            oDataFilter = "";
            return false;
        }

        if (expression is BinaryExpression { NodeType: ExpressionType.Equal } equality)
        {
            return TryCreateEquality(equality.Left, equality.Right, out oDataFilter)
                || TryCreateEquality(equality.Right, equality.Left, out oDataFilter);
        }

        oDataFilter = "";
        return false;
    }

    static bool TryCreateEquality(
        Expression propertyExpression,
        Expression valueExpression,
        out string oDataFilter
    )
    {
        propertyExpression = StripConversion(propertyExpression);
        if (
            propertyExpression is not MemberExpression member
            || member.Expression == null
            || StripConversion(member.Expression) is not ParameterExpression
        )
        {
            oDataFilter = "";
            return false;
        }

        if (!TryEvaluate(valueExpression, out var value))
        {
            oDataFilter = "";
            return false;
        }

        oDataFilter = $"{member.Member.Name} eq {FormatValue(value)}";
        return true;
    }

    static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        return value switch
        {
            string text => $"'{EscapeString(text)}'",
            char character => $"'{EscapeString(character.ToString())}'",
            bool flag => flag ? "true" : "false",
            Enum enumValue => $"'{EscapeString(enumValue.ToString())}'",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => $"'{EscapeString(value.ToString() ?? "")}'",
        };
    }

    static string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    static bool TryEvaluate(Expression expression, out object? value)
    {
        try
        {
            expression = StripConversion(expression);
            if (expression is ConstantExpression constant)
            {
                value = constant.Value;
                return true;
            }

            value = Expression.Lambda<Func<object?>>(
                Expression.Convert(expression, typeof(object))
            )
            .Compile()
            .Invoke();
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    static Expression StripConversion(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }
}
