using NC.Domain;
using NC.Krud.Abstractions;

namespace NC.Krud.Services;

internal sealed class KrudGraphMirror<TRoot, TPrincipal> : IKrudMirrorService<TPrincipal>
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
