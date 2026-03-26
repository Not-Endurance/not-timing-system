using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Domain.Krud;
using Not.Krud.Abstractions;

namespace Not.Krud.Services;

internal sealed class KrudRootMirror<TRoot, TPrincipal> : IKrudMirror<TPrincipal>
    where TRoot : Aggregate, IEntityMirror<TPrincipal>
    where TPrincipal : Entity
{
    readonly List<IKrudMirror<TRoot>> _mirrors;
    readonly IRepository<TRoot> _repository;

    public KrudRootMirror(IRepository<TRoot> repository, IEnumerable<IKrudMirror<TRoot>> mirrors)
    {
        _repository = repository;
        _mirrors = mirrors.ToList();
    }

    public async Task Reflect(TPrincipal entity)
    {
        var roots = await _repository.ReadMany();
        foreach (var root in roots)
        {
            if (!root.Reflect(entity))
            {
                continue;
            }

            await _repository.Update(root);
            foreach (var mirror in _mirrors)
            {
                await mirror.Reflect(root);
            }
        }
    }
}
