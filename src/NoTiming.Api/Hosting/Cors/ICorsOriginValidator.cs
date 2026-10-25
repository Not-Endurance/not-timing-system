namespace NoTiming.Api.Hosting.Cors;

public interface ICorsOriginValidator
{
    bool IsAllowed(string? origin);
}
