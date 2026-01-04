using Not.Injection;

namespace Not.Blazor.Ports;

public interface IPrintInterop : ITransient
{
    Task OpenPrintDialog();
}
