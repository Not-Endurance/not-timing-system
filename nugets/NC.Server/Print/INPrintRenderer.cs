using NC.Injection;

namespace NC.Server.Print;

public interface INPrintRenderer : ISingleton
{
    Task<byte[]> Render(string html, CancellationToken cancellationToken);
}
