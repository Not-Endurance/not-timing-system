using Not.Injection;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public interface IPdfRenderer : ISingleton
{
    Task<byte[]> Render(Uri url, CancellationToken cancellationToken);
}
