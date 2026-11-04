using NC.Injection;

namespace NC.Blazor.Components.Abstractions;

public interface IPrintInterop : ITransient
{
    Task OpenPrintDialog();
}
