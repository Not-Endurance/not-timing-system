using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Domain;
using Not.Exceptions;
using NTS.Domain.Setup.Entities;
using static MudBlazor.CategoryTypes;

namespace NTS.Judge.Events;
public class LoopBehind : INotSetBehind<Loop>
{
    private readonly IRepository<Loop> _loopRepository;
    private Loop? _loop;

    public LoopBehind(IRepository<Loop> loopRepository)
    {
        _loopRepository = loopRepository;
    }

    public Task<IEnumerable<Loop>> GetAll()
    {
        return _loopRepository.ReadAll();
    }

    public async Task<Loop> Create(Loop entity)
    {
        _loop = await _loopRepository.Create(entity);
        return _loop;
    }

    public async Task<Loop> Update(Loop entity)
    {
        return await _loopRepository.Update(entity);
    }

    public async Task<Loop> Delete(Loop entity)
    {
        return await _loopRepository.Delete(entity);
    }
}
