using Not.Events;
using Not.Observables;

namespace Not.Application.Krud.Graph;

public class KrudNode : Observer, IObservable
{
    readonly List<KrudNode> _children = [];

    protected Event StateChanged { get; } = new();

    public object? Value { get; protected set; }
    public IEventSubscriber Event => StateChanged;

    public IReadOnlyList<KrudNode> Children => _children.AsReadOnly();

    internal virtual void AttachChildren(IEnumerable<KrudNode> nodes)
    {
        if (_children.Any())
        {
            return;
        }
        foreach (var node in nodes)
        {
            Observe(node, StateChanged.Emit);
        }
        _children.AddRange(nodes);
    }

    public void Set(object aggregate)
    {
        Value = aggregate;
    }
}

