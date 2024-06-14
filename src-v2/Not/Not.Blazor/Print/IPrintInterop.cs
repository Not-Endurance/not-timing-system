using Not.Injection;

namespace Not.Blazor.Print;

public interface IPrintInterop : ITransientService
{
    Task Print();
}
