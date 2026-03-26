using Not.Domain;
using Not.Krud.Abstractions;

namespace Not.Krud.Services;

internal sealed class KrudGraphMirror<TRoot, TPrincipal> : IKrudMirror<TPrincipal>
    where TRoot : Aggregate
    where TPrincipal : Entity
{
    readonly KrudGraphContext<TRoot> _context;

    public KrudGraphMirror(KrudGraphContext<TRoot> context)
    {
        _context = context;
    }

    public Task Reflect(TPrincipal entity)
    {
        return _context.Reflect(entity);
    }
}
