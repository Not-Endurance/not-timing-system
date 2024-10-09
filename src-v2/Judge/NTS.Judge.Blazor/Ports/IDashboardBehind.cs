using Not.Injection;
namespace NTS.Judge.Blazor.Ports;

public interface IDashboardBehind : ISingletonService
{
    Task Start();
}