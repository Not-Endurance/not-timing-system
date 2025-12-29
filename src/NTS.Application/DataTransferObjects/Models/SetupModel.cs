using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using static NTS.Application.DataTransferObjects.Models.CommonModel;

namespace NTS.Application.DataTransferObjects.Models;

public class SetupModel
{
    public class AthleteModel : Identity
    {
        public static AthleteModel Create(IAthlete athlete)
        {
            return new AthleteModel
            {
                Id = athlete.Id,
                FeiId = athlete.FeiId,
                Names = athlete.Names,
                Country = CountryModel.Create(athlete.Country),
                Club = athlete.Club == null ? null : ClubModel.Create(athlete.Club),
            };
        }

        public string[] Names { get; init; } = default!;
        public CountryModel? Country { get; init; } // TODO: should be required
        public ClubModel? Club { get; init; }
        public string? FeiId { get; init; }

        public Athlete ToSetupDomain()
        {
            return new Athlete(Id, Names, FeiId, Country?.ToDomain(), Club?.ToDomain());
        }
    }

    public class HorseModel : Identity
    {
        public static HorseModel Create(IHorse horse)
        {
            return new HorseModel
            {
                Id = horse.Id,
                FeiId = horse.FeiId,
                Name = horse.Name,
            };
        }

        public string? FeiId { get; init; }
        public string Name { get; init; } = default!;

        public Horse ToSetupDomain()
        {
            return new Horse(Id, Name, FeiId);
        }
    }

    public class ClubModel : Identity
    {
        public static ClubModel Create(IClub club)
        {
            return new ClubModel { Id = club.Id, Name = club.Name };
        }

        public string Name { get; init; } = default!;

        public Club ToDomain()
        {
            return new Club(Id, Name);
        }
    }

    public class LoopModel
    {
        public static LoopModel Create(Loop loop)
        {
            return new() { Id = loop.Id, Distance = loop.Distance };
        }

        public int Id { get; init; }
        public double Distance { get; init; }

        public Loop ToSetupDomain()
        {
            return Loop.Update(EnsureId(Id), Distance);
        }
    }

    public class UpcomingEventModel : Identity
    {
        public static UpcomingEventModel Create(UpcomingEvent @event)
        {
            return new UpcomingEventModel
            {
                Id = @event.Id,
                Name = @event.Name,
                Place = @event.Place,
                Country = CountryModel.Create(@event.Country),
                ShowFeiId = @event.ShowFeiId,
                FeiId = @event.FeiId,
                FeiEventCode = @event.FeiEventCode,
                Competitions = @event.Competitions.Select(CompetitionModel.Create).ToArray(),
                Officials = @event.Officials.Select(OfficialModel.Create).ToArray(),
                Loops = @event.Loops.Select(LoopModel.Create).ToArray(),
                Combinations = @event.Combinations.Select(CombinationModel.Create).ToArray(),
            };
        }

        public string Place { get; init; } = default!;
        public CountryModel Country { get; init; } = default!;
        public string? ShowFeiId { get; init; }
        public string? FeiId { get; init; }
        public string? FeiEventCode { get; init; }
        public CompetitionModel[] Competitions { get; init; } = default!;
        public OfficialModel[] Officials { get; init; } = default!;
        public LoopModel[] Loops { get; init; } = default!;
        public CombinationModel[] Combinations { get; init; } = default!;
        public string Name { get; init; } = default!;

        public UpcomingEvent ToDomain()
        {
            return new UpcomingEvent(
                Id,
                Name,
                Place,
                Country.ToDomain(),
                ShowFeiId,
                FeiId,
                FeiEventCode,
                Competitions.Select(x => x.ToSetupDomain()),
                Officials.Select(x => x.ToSetupDomain()),
                Loops.Select(x => x.ToSetupDomain()),
                Combinations.Select(x => x.ToSetupDomain())
            );
        }
    }
}
