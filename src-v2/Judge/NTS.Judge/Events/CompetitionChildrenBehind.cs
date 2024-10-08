using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Setup.Entities;

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

    Task<IEnumerable<Contestant>> SafeGetAllContestants()
    {
        return Task.FromResult(_competition?.Contestants.AsEnumerable() ?? []);
    }

    Task<IEnumerable<Phase>> SafeGetAllPhases()
    {
        return Task.FromResult(_competition?.Phases.AsEnumerable() ?? []);
    }

    async Task<Contestant> SafeCreate(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Add(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Contestant> SafeUpdate(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Update(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Contestant> SafeDelete(Contestant entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Remove(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Phase> SafeCreate(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Add(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Phase> SafeUpdate(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Update(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Phase> SafeDelete(Phase entity)
    {
        GuardHelper.ThrowIfDefault(_competition);

        _competition.Remove(entity);
        await _competitionRepository.Update(_competition);
        return entity;
    }

    async Task<Competition?> SafeInitialize(int id)
    {
        return _competition = await _competitionReader.Read(id);
    }

    #region SafePatter

    async Task<IEnumerable<Contestant>> IReadAllBehind<Contestant>.GetAll()
    {
        return await SafeHelper.Run(SafeGetAllContestants) ?? [];
    }

    async Task<IEnumerable<Phase>> IReadAllBehind<Phase>.GetAll()
    {
        return await SafeHelper.Run(SafeGetAllPhases) ?? [];
    }

    public async Task<Contestant> Create(Contestant contestant)
    {
        return await SafeHelper.Run(() => SafeCreate(contestant)) ?? contestant;
    }

    public async Task<Contestant> Update(Contestant contestant)
    {
        return await SafeHelper.Run(() => SafeUpdate(contestant)) ?? contestant;
    }

    public async Task<Contestant> Delete(Contestant contestant)
    {
        return await SafeHelper.Run(() => SafeDelete(contestant)) ?? contestant;
    }

    public async Task<Phase> Create(Phase phase)
    {
        return await SafeHelper.Run(() => SafeCreate(phase)) ?? phase;
    }

    public async Task<Phase> Update(Phase phase)
    {
        return await SafeHelper.Run(() => SafeUpdate(phase)) ?? phase;
    }

    public async Task<Phase> Delete(Phase phase)
    {
        return await SafeHelper.Run(() => Delete(phase)) ?? phase;
    }

    public async Task<Competition?> Initialize(int id)
    {
        return await SafeHelper.Run(() => SafeInitialize(id)); 
    }

    #endregion
}
