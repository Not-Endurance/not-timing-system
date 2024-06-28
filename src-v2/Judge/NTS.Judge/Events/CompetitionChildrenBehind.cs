using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Exceptions;
using NTS.Domain.Setup.Entities;
using System.Collections.Generic;

namespace NTS.Judge.Events;
public class CompetitionChildrenBehind : INotSetBehind<Contestant>, INotSetBehind<Phase>, INotParentBehind<Competition>
{
    private readonly IRead<Competition> _competitionReader;
    private readonly IRepository<Competition> _competitionRepository;
    private Competition? _competition;

    public CompetitionChildrenBehind(IRead<Competition> competitionReader, IRepository<Competition> competitionRepository) 
    {
        _competitionReader = competitionReader;
        _competitionRepository = competitionRepository;
    }

    public async Task<IEnumerable<Contestant>> GetAll()
    {
        return _competition?.Contestants ?? Enumerable.Empty<Contestant>();
    }

    async Task<IEnumerable<Phase>> IReadAllBehind<Phase>.GetAll()
    {
        return _competition?.Phases ?? Enumerable.Empty<Phase>();
    }

    public async Task<Contestant> Create(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Add(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Contestant> Update(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Update(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Contestant> Delete(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Remove(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Phase> Create(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Add(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Phase> Update(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Update(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Phase> Delete(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Remove(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    public async Task<Competition> Initialize(int id)
    {
        _competition = await _competitionReader.Read(id);
        GuardHelper.ThrowIfDefault(_competition);
        return _competition;
    }
}
