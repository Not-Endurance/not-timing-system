namespace NTS.Nexus.Warp.Cors;

public interface ICorsOriginValidator
{
    bool IsAllowed(string? origin);
}
