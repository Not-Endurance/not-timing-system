namespace NTS.Application.Cors;

public interface ICorsOriginValidator
{
    bool IsAllowed(string? origin);
}
