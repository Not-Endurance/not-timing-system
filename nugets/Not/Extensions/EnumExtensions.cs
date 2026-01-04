using System.ComponentModel;
using Not.Reflection;

namespace Not.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var type = value.GetType();
        return
            ReflectionHelper
                .GetEnumField(type, value)
                ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault()
                is not DescriptionAttribute descriptionAttribute
            ? value.ToString()
            : descriptionAttribute.Description;
    }
}
