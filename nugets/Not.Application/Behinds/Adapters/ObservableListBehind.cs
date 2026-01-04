using Not.Structures;

namespace Not.Application.Behinds.Adapters;

public abstract class ObservableListBehind<T> : ObservableBehind, IDisposable
    where T : IIdentifiable
{
    readonly Guid _loadedSubscriptionId;

    protected ObservableListBehind()
    {
        _loadedSubscriptionId = ObservableList.ChangedEvent.Subscribe(EmitChange);
    }

    protected ObservableList<T> ObservableList { get; } = [];

    public void Dispose()
    {
        ObservableList.ChangedEvent.Unsubscribe(_loadedSubscriptionId);
        GC.SuppressFinalize(this);
    }
}
