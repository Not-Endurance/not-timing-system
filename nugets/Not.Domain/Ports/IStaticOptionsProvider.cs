namespace Not.Domain.Ports;

public interface IStaticOptionsProvider<T>
{
    T Get();
}
