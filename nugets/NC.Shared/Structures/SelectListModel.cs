using Not.Extensions;

namespace Not.Structures;

// TODO combine with NotListModel
public class SelectListModel
{
    public static IEnumerable<SelectListModel<T>> FromEnum<T>()
        where T : struct, Enum
    {
        var enumValues = Enum.GetValues<T>();
        var selectItems = enumValues.Select(s => new SelectListModel<T>(s, s.GetDescription()));
        return selectItems;
    }
}

public class SelectListModel<T>
{
    public SelectListModel(T value, string description)
    {
        Value = value;
        Description = description;
    }

    public T Value { get; set; }
    public string Description { get; set; }
}
