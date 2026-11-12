namespace NoTiming.Ui.Application.Features.Account;

/// <summary>Adds the header the server's state-change guard requires on every write, so the browser's own requests pass it.</summary>
public sealed class UiRequestGuardHandler : DelegatingHandler
{
    public const string HEADER = "X-NTS-Request";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.Method != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation(HEADER, "1");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
