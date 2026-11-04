namespace NTS.Nexus.Warp.Host.Cors;

public interface ICorsOriginValidator
{
    bool IsAllowed(string? origin);
}
