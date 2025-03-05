using Not.Injection;

namespace Not.Blazor.Ports;

public interface ISingleParentContext : ISingleton
{
    void Update(object child);
}
