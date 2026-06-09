using Not.Injection;

namespace Not.Server.Print;

public interface INPrintRenderer : ISingleton
{
    Task<byte[]> Render(string html, CancellationToken cancellationToken);
}
