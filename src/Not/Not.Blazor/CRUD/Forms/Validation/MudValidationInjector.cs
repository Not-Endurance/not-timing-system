using System.Reflection;
using MudBlazor;
using Not.Blazor.Components;
using Not.Reflection;

namespace Not.Blazor.CRUD.Forms.Validation;

// Breaks Not* convention, because this is a workaround until
// event-driven validation is implemented for MudBaseInput<T>
public class MudValidationInjector
{
    static readonly Type _listOfStringType = typeof(List<string>);

    public static MudValidationInjector Create<T>(Func<MudFormComponent<T, string>> getter)
    {
        return new MudValidationInjector(typeof(MudFormComponent<T, string>), getter);
    }

    public static MudValidationInjector Create<T>(Func<MudBooleanInput<T>> wrapperGetter)
    {
        return new MudValidationInjector(typeof(MudFormComponent<T, string>), () => wrapperGetter());
    }

    public static MudValidationInjector Create<T, TInternal>(Func<NSwitch> wrapperGetter)
    {
        return new MudValidationInjector(typeof(NSwitch), () => wrapperGetter());
    }

    public static MudValidationInjector Create<T>(Func<IMudBaseInputWrapper<T>> wrapperGetter)
    {
        return new MudValidationInjector(typeof(MudFormComponent<T, string>), () => wrapperGetter().MudBaseInput);
    }

    // Change to use MudFormComponent<T, U>
    MudValidationInjector(Type mudInputType, Func<object> mudInputInstanceGetter)
    {
        InstanceGetter = mudInputInstanceGetter;
        // Generic parameter doesn't matter here as it's only used to obtain property names with nameof()

        ErrorProperty = mudInputType.Property("Error");
        ErrorTextProperty = mudInputType.Property("ErrorText");
        ValidationErrorsProperty = mudInputType.Property("ValidationErrors");
        AddValidationErrorMethod = _listOfStringType.Method(nameof(List<string>.Add));
    }

    protected Func<object> InstanceGetter { get; }
    protected PropertyInfo ErrorProperty { get; }
    protected PropertyInfo ErrorTextProperty { get; }
    protected PropertyInfo ValidationErrorsProperty { get; }
    protected MethodInfo AddValidationErrorMethod { get; }

    public void Inject(string message)
    {
        var mudBaseInput = InstanceGetter();

        ErrorProperty.Set(mudBaseInput, true);
        ErrorTextProperty.Set(mudBaseInput, message);
        var validationErrorsList =
            ValidationErrorsProperty.Get(mudBaseInput)
            ?? throw new Exception($"MudBlazor input component's ValidationErrors property is null'");
        AddValidationErrorMethod.Invoke(validationErrorsList, new object[] { message });
    }
}
