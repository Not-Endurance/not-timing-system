using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Safe;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Events;

public class LoopBehind : INotSetBehind<Loop>
{
    private readonly IRepository<Loop> _loopRepository;

    public LoopBehind(IRepository<Loop> loopRepository)
    {
        _loopRepository = loopRepository;
    }

    Task<IEnumerable<Loop>> SafeGetAll()
    {
        return _loopRepository.ReadAll();
    }

    Task<Loop> SafeCreate(Loop entity)
    {
        return _loopRepository.Create(entity);
    }

    Task<Loop> SafeUpdate(Loop entity)
    {
        return _loopRepository.Update(entity);
    }

    Task<Loop> SafeDelete(Loop entity)
    {
        return _loopRepository.Delete(entity);
    }

    #region SafePattern

    public async Task<IEnumerable<Loop>> GetAll()
    {
        return await SafeHelper.Run(SafeGetAll) ?? [];
    }

    public async Task<Loop> Create(Loop loop)
    {
        return await SafeHelper.Run(() => SafeCreate(loop)) ?? loop;
    }

    public async Task<Loop> Update(Loop loop)
    {
        return await SafeHelper.Run(() => SafeUpdate(loop)) ?? loop;
    }

    public async Task<Loop> Delete(Loop loop)
    {
        return await SafeHelper.Run(() => SafeDelete(loop)) ?? loop;
    }

    #endregion
}
