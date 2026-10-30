using Not.Async;

namespace NTS.Tests.Unit.Harness;

internal sealed class CountingWork : IRetryable
{
    readonly int _answerAfter;

    public CountingWork(int answerAfter)
    {
        _answerAfter = answerAfter;
    }

    public bool CanRetry { get; init; } = true;
    public Exception? Fails { get; init; }
    public int Tries { get; private set; }

    public Task<bool> RetryAsync(CancellationToken cancellationToken)
    {
        Tries++;
        if (Fails != null)
        {
            throw Fails;
        }

        return Task.FromResult(Tries >= _answerAfter);
    }
}
