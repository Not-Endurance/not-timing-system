using Newtonsoft.Json;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using NTS.Judge.Contracts.Features.Setup.Athletes;
using NTS.Judge.Contracts.Features.Setup.Clubs;
using NTS.Judge.Contracts.Features.Setup.Horses;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Combinations;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Loops;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Officials;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Participations;
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Phases;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.EndToEndEventTests.Helpers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.EndToEndEventTests.Features;

internal class ConfigureEventFeature
{
    readonly JudgeDriver _judge;
    readonly NexusApiDriver _nexusApi;

    public ConfigureEventFeature(JudgeDriver judge, NexusApiDriver nexusApi)
    {
        _judge = judge;
        _nexusApi = nexusApi;
    }

    public async Task<SetupFeatureResult> Execute(EndToEndEventSnapshot eventSnapshot)
    {
        return await ConfigureEventSnapshotCore(eventSnapshot);
    }

    async Task<SetupFeatureResult> ConfigureEventSnapshotCore(EndToEndEventSnapshot snapshot)
    {
        var idMap = new Dictionary<int, int>();
        var createdClubs = new Dictionary<int, Club>();
        var createdHorses = new Dictionary<int, Horse>();
        var createdAthletes = new Dictionary<int, Athlete>();
        var createdUsers = new Dictionary<int, User>();
        var createdLoops = new Dictionary<int, Loop>();
        var createdCombinations = new Dictionary<int, Combination>();

        foreach (var user in snapshot.Users)
        {
            var registered = await _nexusApi.RegisterUser(
                new IntegrationUser(
                    user.Email,
                    $"setup-user-{user.Id}",
                    user.Name,
                    user.GivenName,
                    user.MiddleName,
                    user.Surname,
                    user.CountryRegion,
                    user.Club,
                    user.FeiId
                )
            );
            var created = new User(
                registered.Email,
                registered.Name,
                registered.Roles,
                registered.Id,
                registered.GivenName,
                registered.MiddleName,
                registered.Surname,
                registered.CountryRegion,
                registered.Club,
                registered.FeiId
            );
            Remember(idMap, user.Id, created.Id);
            createdUsers.Add(user.Id, created);
        }

        var clubService = _judge.GetRequiredService<IKrudFormService<ClubFormModel>>();
        foreach (var club in snapshot.Clubs)
        {
            var form = new ClubFormModel { Name = club.Name };
            await clubService.Create(form);

            var created = new Club(club.Name, RequiredId(form));
            Remember(idMap, club.Id, created.Id);
            createdClubs.Add(club.Id, created);

            var persisted = await _nexusApi.ReadSetupClubs();
            Assert.Contains(persisted, x => x.Id == created.Id && x.Name == club.Name);
        }

        var horseService = _judge.GetRequiredService<IKrudFormService<HorseFormModel>>();
        foreach (var horse in snapshot.Horses)
        {
            var form = new HorseFormModel { Name = horse.Name, FeiId = horse.FeiId };
            await horseService.Create(form);

            var created = new Horse(horse.Name, horse.FeiId, RequiredId(form));
            Remember(idMap, horse.Id, created.Id);
            createdHorses.Add(horse.Id, created);

            var persisted = await _nexusApi.ReadSetupHorses();
            Assert.Contains(
                persisted,
                x => x.Id == created.Id && x.Name == horse.Name && x.FeiId == horse.FeiId
            );
        }

        var athleteService = _judge.GetRequiredService<IKrudFormService<AthleteFormModel>>();
        foreach (var athlete in snapshot.Athletes)
        {
            var form = new AthleteFormModel
            {
                Names = athlete.Names.ToString(),
                FeiId = athlete.FeiId,
                Country = athlete.Country,
                Club = athlete.Club == null ? null : createdClubs[athlete.Club.Id],
                User = athlete.User == null ? null : createdUsers[athlete.User.Id],
            };
            await athleteService.Create(form);

            var created = new Athlete(
                athlete.Names,
                athlete.FeiId,
                athlete.Country,
                athlete.Club == null ? null : createdClubs[athlete.Club.Id],
                RequiredId(form),
                athlete.User == null ? null : createdUsers[athlete.User.Id]
            );
            Remember(idMap, athlete.Id, created.Id);
            createdAthletes.Add(athlete.Id, created);

            var persisted = await _nexusApi.ReadSetupAthletes();
            Assert.Contains(
                persisted,
                x =>
                    x.Id == created.Id
                    && x.Names.ToString() == athlete.Names.ToString()
                    && x.Country.Id == athlete.Country.Id
                    && x.Club?.Id == created.Club?.Id
            );
        }

        var eventService = _judge.GetRequiredService<IKrudFormService<ConfigureEventFormModel>>();
        var eventForm = new ConfigureEventFormModel
        {
            Name = snapshot.ConfigureEvent.Name,
            Location = snapshot.ConfigureEvent.Location,
            Country = snapshot.ConfigureEvent.Country,
            FeiShowId = snapshot.ConfigureEvent.ShowFeiId,
            FeiId = snapshot.ConfigureEvent.FeiId,
            FeiEventCode = snapshot.ConfigureEvent.FeiEventCode,
        };
        await eventService.Create(eventForm);
        var setupEventId = RequiredId(eventForm);
        Remember(idMap, snapshot.ConfigureEvent.Id, setupEventId);

        var currentEvent = await _nexusApi.ReadSetupConfigureEvent(setupEventId);
        Assert.Equal(snapshot.ConfigureEvent.Name, currentEvent.Name);
        Assert.Empty(currentEvent.Loops);
        Assert.Empty(currentEvent.Combinations);
        Assert.Empty(currentEvent.Officials);
        Assert.Empty(currentEvent.Competitions);
        _judge.SelectSetupParent(currentEvent);

        var loopService = _judge.GetRequiredService<IKrudFormService<LoopFormModel>>();
        foreach (var loop in snapshot.ConfigureEvent.Loops)
        {
            var form = new LoopFormModel { Distance = loop.Distance };
            await loopService.Create(form);

            currentEvent = await WaitForSetupEvent(
                _nexusApi,
                setupEventId,
                setupEvent => setupEvent.Loops.Any(x => x.Distance == loop.Distance),
                $"loop {loop.Distance}"
            );
            var created = currentEvent.Loops.Single(x => x.Distance == loop.Distance);
            Remember(idMap, loop.Id, created.Id);
            createdLoops.Add(loop.Id, created);

            Assert.Contains(currentEvent.Loops, x => x.Id == created.Id && x.Distance == loop.Distance);
            _judge.SelectSetupParent(currentEvent);
        }

        var combinationService = _judge.GetRequiredService<IKrudFormService<CombinationFormModel>>();
        foreach (var combination in snapshot.ConfigureEvent.Combinations)
        {
            var form = new CombinationFormModel
            {
                Number = combination.Number,
                Athlete = createdAthletes[combination.Athlete.Id],
                Horse = createdHorses[combination.Horse.Id],
            };
            await combinationService.Create(form);

            var created = new Combination(
                combination.Number,
                createdAthletes[combination.Athlete.Id],
                createdHorses[combination.Horse.Id],
                RequiredId(form)
            );
            Remember(idMap, combination.Id, created.Id);
            createdCombinations.Add(combination.Id, created);

            currentEvent = await WaitForSetupEvent(
                _nexusApi,
                setupEventId,
                setupEvent => setupEvent.Combinations.Any(x => x.Id == created.Id),
                $"combination {combination.Number}"
            );
            Assert.Contains(
                currentEvent.Combinations,
                x =>
                    x.Id == created.Id
                    && x.Number == combination.Number
                    && x.Athlete.Id == created.Athlete.Id
                    && x.Horse.Id == created.Horse.Id
            );
            _judge.SelectSetupParent(currentEvent);
        }

        var officialService = _judge.GetRequiredService<IKrudFormService<OfficialFormModel>>();
        foreach (var official in snapshot.ConfigureEvent.Officials)
        {
            var form = new OfficialFormModel
            {
                Name = official.Person.ToString(),
                Role = official.Role,
                User = official.User == null ? null : createdUsers[official.User.Id],
            };
            await officialService.Create(form);
            var officialId = RequiredId(form);
            Remember(idMap, official.Id, officialId);

            currentEvent = await WaitForSetupEvent(
                _nexusApi,
                setupEventId,
                setupEvent => setupEvent.Officials.Any(x => x.Id == officialId),
                $"official {official.Person}"
            );
            Assert.Contains(
                currentEvent.Officials,
                x =>
                    x.Id == officialId
                    && x.Person.ToString() == official.Person.ToString()
                    && x.Role == official.Role
            );
            _judge.SelectSetupParent(currentEvent);
        }

        var competitionService = _judge.GetRequiredService<IKrudFormService<CompetitionFormModel>>();
        var phaseService = _judge.GetRequiredService<IKrudFormService<PhaseFormModel>>();
        var participationService = _judge.GetRequiredService<IKrudFormService<ParticipationFormModel>>();

        foreach (var competition in snapshot.ConfigureEvent.Competitions)
        {
            var competitionForm = CreateCompetitionForm(competition);
            await competitionService.Create(competitionForm);
            var competitionId = RequiredId(competitionForm);
            Remember(idMap, competition.Id, competitionId);

            currentEvent = await WaitForSetupEvent(
                _nexusApi,
                setupEventId,
                setupEvent => setupEvent.Competitions.Any(x => x.Id == competitionId),
                $"competition {competition.Name}"
            );
            Assert.Contains(
                currentEvent.Competitions,
                x =>
                    x.Id == competitionId
                    && x.Name == competition.Name
                    && x.Type == competition.Type
                    && x.Ruleset == competition.Ruleset
            );

            SelectCompetition(_judge, currentEvent, competitionId);

            foreach (var phase in competition.Phases)
            {
                var phaseLoop = new Loop(phase.Loop.Distance, idMap[phase.Loop.Id]);
                var phaseForm = new PhaseFormModel
                {
                    Loop = phaseLoop,
                    Recovery = phase.Recovery,
                    Rest = phase.Rest,
                };
                await phaseService.Create(phaseForm);
                var phaseId = RequiredId(phaseForm);
                Remember(idMap, phase.Id, phaseId);

                currentEvent = await WaitForSetupEvent(
                    _nexusApi,
                    setupEventId,
                    setupEvent =>
                        setupEvent.Competitions.Any(x =>
                            x.Id == competitionId && x.Phases.Any(y => y.Id == phaseId)
                        ),
                    $"phase {phase.Id}"
                );
                var persistedCompetition = FindCompetition(currentEvent, competitionId);
                Assert.Contains(
                    persistedCompetition.Phases,
                    x =>
                        x.Id == phaseId
                        && x.Recovery == phase.Recovery
                        && x.Rest == phase.Rest
                        && x.Loop.Id == idMap[phase.Loop.Id]
                );
                SelectCompetition(_judge, currentEvent, competitionId);
            }

            foreach (var participation in competition.Participations)
            {
                var participationForm = CreateParticipationForm(participation, createdCombinations);
                await participationService.Create(participationForm);
                var participationId = RequiredId(participationForm);
                Remember(idMap, participation.Id, participationId);

                currentEvent = await WaitForSetupEvent(
                    _nexusApi,
                    setupEventId,
                    setupEvent =>
                        setupEvent.Competitions.Any(x =>
                            x.Id == competitionId && x.Participations.Any(y => y.Id == participationId)
                        ),
                    $"participation {participation.Combination.Number}"
                );
                var persistedCompetition = FindCompetition(currentEvent, competitionId);
                Assert.Contains(
                    persistedCompetition.Participations,
                    x =>
                        x.Id == participationId
                        && x.Category == participation.Category
                        && x.Combination.Id == createdCombinations[participation.Combination.Id].Id
                );
                SelectCompetition(_judge, currentEvent, competitionId);
            }
        }

        currentEvent = await _nexusApi.ReadSetupConfigureEvent(setupEventId);
        var expected = snapshot.ExpectedConfigureEventWith(idMap);
        var actual = SnapshotJson.Canonicalize(ConfigureEventModel.From(currentEvent));

        Assert.Equal(expected.ToString(Formatting.None), actual.ToString(Formatting.None));
        return new SetupFeatureResult(currentEvent, new Dictionary<int, int>(idMap), ResolveWitnessOfficial(currentEvent));
    }

    static IntegrationUser ResolveWitnessOfficial(ConfigureEvent setupEvent)
    {
        var user = setupEvent.Officials.Select(x => x.User).FirstOrDefault(x => x != null);
        if (user == null)
        {
            throw new InvalidOperationException("The setup event snapshot does not include a linked official user.");
        }

        return new IntegrationUser(
            user.Email,
            $"setup-official-{user.Id}",
            user.Name,
            user.GivenName,
            user.MiddleName,
            user.Surname,
            user.CountryRegion,
            user.Club,
            user.FeiId
        );
    }

    static async Task<ConfigureEvent> WaitForSetupEvent(
        NexusApiDriver api,
        int setupEventId,
        Func<ConfigureEvent, bool> predicate,
        string expectedState
    )
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        ConfigureEvent? current = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            current = await api.ReadSetupConfigureEvent(setupEventId);
            if (predicate(current))
            {
                return current;
            }

            await Task.Delay(50);
        }

        var summary =
            current == null
                ? "no event was returned"
                : $"{current.Loops.Count} loop(s), {current.Combinations.Count} combination(s), {current.Officials.Count} official(s), {current.Competitions.Count} competition(s)";
        throw new TimeoutException($"Setup event {setupEventId} did not persist {expectedState}: {summary}.");
    }

    static CompetitionFormModel CreateCompetitionForm(Competition competition)
    {
        var localStart = competition.Start.ToLocalTime();
        return new CompetitionFormModel
        {
            Name = competition.Name,
            Type = competition.Type,
            Ruleset = competition.Ruleset,
            Date = localStart.Date,
            Time = localStart.TimeOfDay,
            UseCompulsoryThreshold = competition.CompulsoryThresholdSpan != null,
            CompulsoryThresholdMinutes = competition.CompulsoryThresholdSpan == null
                ? null
                : (int)competition.CompulsoryThresholdSpan.Value.TotalMinutes,
            FeiId = competition.FeiId,
            FeiRule = competition.FeiRule,
            FeiScheduleNumber = competition.FeiScheduleNumber,
        };
    }

    static ParticipationFormModel CreateParticipationForm(
        Participation participation,
        IReadOnlyDictionary<int, Combination> createdCombinations
    )
    {
        var localStart = participation.StartTimeOverride?.ToLocalTime();
        return new ParticipationFormModel
        {
            IsNotRanked = participation.IsNotRanked,
            Category = participation.Category,
            Combination = createdCombinations[participation.Combination.Id],
            IsStartTimeOverriden = participation.StartTimeOverride != null,
            StartTimeOverride = localStart?.TimeOfDay,
            IsMaxSpeedOverriden = participation.MaxSpeedOverride != null,
            MaxSpeedOverride = participation.MaxSpeedOverride,
            IsMinSpeedOverriden = participation.MinSpeedOverride != null,
            MinSpeedOverride = participation.MinSpeedOverride,
        };
    }

    static void SelectCompetition(JudgeDriver judge, ConfigureEvent setupEvent, int competitionId)
    {
        judge.SelectSetupParent(setupEvent);
        judge.SelectSetupParent(FindCompetition(setupEvent, competitionId));
    }

    static Competition FindCompetition(ConfigureEvent setupEvent, int competitionId)
    {
        return setupEvent.Competitions.Single(x => x.Id == competitionId);
    }

    static int RequiredId(IKrudFormModel form)
    {
        return form.Id ?? throw new InvalidOperationException($"{form.GetType().Name} did not receive an id.");
    }

    static void Remember(IDictionary<int, int> ids, int original, int created)
    {
        ids[original] = created;
    }
}

internal sealed class SetupFeatureResult
{
    public SetupFeatureResult(
        ConfigureEvent setupEvent,
        IReadOnlyDictionary<int, int> idMap,
        IntegrationUser witnessOfficial
    )
    {
        SetupEvent = setupEvent;
        IdMap = idMap;
        WitnessOfficial = witnessOfficial;
    }

    public ConfigureEvent SetupEvent { get; }
    public IReadOnlyDictionary<int, int> IdMap { get; }
    public IntegrationUser WitnessOfficial { get; }
}
