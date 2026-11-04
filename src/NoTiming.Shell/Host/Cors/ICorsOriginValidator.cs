namespace NoTiming.Host.Cors;

public interface ICorsOriginValidator
{
    bool IsAllowed(string? origin);
}
