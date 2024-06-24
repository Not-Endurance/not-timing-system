using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Domain;
using Not.Exceptions;
using NTS.Domain.Setup.Entities;
using static MudBlazor.CategoryTypes;

namespace NTS.Judge.Events;
public class AthleteBehind : INotSetBehind<Athlete>
{
    private readonly IRepository<Athlete> _athleteRepository;
    private Athlete? _athlete;

    public AthleteBehind(IRepository<Athlete> athleteRepository)
    {
        _athleteRepository = athleteRepository;
    }

    public Task<IEnumerable<Athlete>> GetAll()
    {
        return _athleteRepository.ReadAll();
    }

    public async Task<Athlete> Create(Athlete entity)
    {
        _athlete = await _athleteRepository.Create(entity);
        return _athlete;
    }

    public async Task<Athlete> Update(Athlete entity)
    {
        return await _athleteRepository.Update(entity);
    }

    public async Task<Athlete> Delete(Athlete entity)
    {
        return await _athleteRepository.Delete(entity);
    }
}
