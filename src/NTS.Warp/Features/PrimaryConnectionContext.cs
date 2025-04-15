using Not.Injection;

namespace NTS.Warp.Features;

public class JudgeConnectionContext : IJudgeConnectionContext
{
    public string? Id { get; set; }
}

public interface IJudgeConnectionContext : ISingleton
{
    string? Id { get; }
}
