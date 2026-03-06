using Not.Injection;

namespace Not.Blazor.Components.Abstractions;

public interface IPrintInterop : ITransient
{
    Task OpenPrintDialog();
}
