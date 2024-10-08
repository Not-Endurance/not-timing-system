using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Safe;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Events;

public class CombinationBehind : INotSetBehind<Combination>
{
    private readonly IRepository<Combination> _combinationRepository;

    public CombinationBehind(IRepository<Combination> combinationRepository)
    {
        _combinationRepository = combinationRepository;
    }

    Task<IEnumerable<Combination>> SafeGetAll()
    {
        return _combinationRepository.ReadAll();
    }

    Task<Combination> SafeCreate(Combination entity)
    {
        return _combinationRepository.Create(entity);
    }

    Task<Combination> SafeUpdate(Combination entity)
    {
        return _combinationRepository.Update(entity);
    }

    Task<Combination> SafeDelete(Combination entity)
    {
        return _combinationRepository.Delete(entity);
    }

    #region SafePattern

    public async Task<IEnumerable<Combination>> GetAll()
    {
        return await SafeHelper.Run(SafeGetAll) ?? [];
    }

    public async Task<Combination> Create(Combination entity)
    {
        return await SafeHelper.Run(() => SafeCreate(entity)) ?? entity;
    }

    public async Task<Combination> Update(Combination entity)
    {
        return await SafeHelper.Run(() => SafeUpdate(entity)) ?? entity;
    }

    public async Task<Combination> Delete(Combination entity)
    {
        return await SafeHelper.Run(() => SafeDelete(entity)) ?? entity;
    }

    #endregion
}
