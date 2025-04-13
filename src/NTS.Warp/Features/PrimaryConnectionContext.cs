using Not.Injection;

namespace NTS.Warp.Features;

public class PrimaryConnectionContext : IPrimaryConnectionContext
{
    public string? Id { get; set; }
}

public interface IPrimaryConnectionContext : ISingleton
{
    string? Id { get; }
}
