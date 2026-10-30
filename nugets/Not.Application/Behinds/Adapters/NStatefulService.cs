using Not.Async;
using Not.Events;
using Not.Observables;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

public abstract class NStatefulService : Observer, IStatefulService
{
    readonly Gate _gate = new();
    readonly Event _changed = new();
    bool _hasLoaded;
    int _resets;

    public IEventSubscriber ObservableEvent => _changed;

    /// <summary>
    /// Creates the service state. Called internally by <see cref="Load"/> which
    /// guarantees single execution and prevents concurrency issues
    /// </summary>
    /// <returns>Indicates weather or not the state has been initialized successfully</returns>
    protected virtual Task<bool> InitializeState()
    {
        return Task.FromResult(true);
    }

    protected void EmitChanged()
    {
        _changed.Emit();
    }

    protected virtual async Task ReloadState()
    {
        ResetHasLoaded();
        await Load();
        EmitChanged();
    }

    protected virtual void ClearState()
    {
        ResetHasLoaded();
        EmitChanged();
    }

    /// <summary>
    /// Resets the service state, which will cause <seealso cref="InitializeState"/> to execute again on next Render.
    /// </summary>
    public void ResetHasLoaded()
    {
        Interlocked.Increment(ref _resets);
        _hasLoaded = false;
    }

    public async Task Load()
    {
        try
        {
            await _gate.WaitToEnter();
            if (_hasLoaded)
            {
                return;
            }

            // A reset that comes while the state is being made says that what is being made is already out of date: it
            // is not taken for loaded, so that the load that asked for the reset (and is waiting at the gate) makes it again.
            var resets = Volatile.Read(ref _resets);
            var isLoaded = await InitializeState();
            _hasLoaded = isLoaded && resets == Volatile.Read(ref _resets);
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
        finally
        {
            _gate.Exit();
        }
    }
}
