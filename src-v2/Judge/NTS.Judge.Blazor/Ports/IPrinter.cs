using Not.Injection;

namespace NTS.Judge.Blazor.Ports;

public interface IPrinter : ITransientService
{
    Task NavigateToPrint();
}
