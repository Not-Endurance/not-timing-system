using Not.Localization;

namespace NTS.Domain.Core.Aggregates.Participations;

// TODO: probably shoudl be a record
public class Tandem : DomainEntity
{
    const double CHILDREN_MIN_SPEED = 8;
    const double CHILDREN_MAX_SPEED = 12;
    const double QUALIFICATION_MIN_SPEED = 10;
    const double QUALIFICATION_MAX_SPEED = 16;
    //const double STARLEVEL_MAX_SPEED = 33;
    private decimal _distance;

    private Tandem(int id) : base(id)
    {
    }
    public Tandem(
        int number,
        Person name,
        string horse,
        decimal distance,
        Country? country,
        Club? club,
        AthleteCategory? athleteCategory,
        CompetitionType? competitionType,
        double? maxSpeedOverride)
    {

        Number = number;
        Name = name;
        Horse = horse;
        _distance = distance;
        Country = country;
        Club = club;
        if (athleteCategory == AthleteCategory.Children)
        {
            MinAverageSpeed = Speed.Create(CHILDREN_MIN_SPEED);
            MaxAverageSpeed = Speed.Create(CHILDREN_MAX_SPEED);
        }
        else
        {
            if (competitionType == CompetitionType.Qualification)
            {
                MinAverageSpeed = Speed.Create(QUALIFICATION_MIN_SPEED);
                MaxAverageSpeed = Speed.Create(QUALIFICATION_MAX_SPEED);
            }
            else
            {
                MinAverageSpeed = null; // check rules for param value Speed.Create(STARLEVEL_MAX_SPEED);
                MaxAverageSpeed = null;
            }
        }
        MaxAverageSpeed = maxSpeedOverride == null ? MaxAverageSpeed : Speed.Create(maxSpeedOverride);
    }

    public int Number { get; private set; }
    public Person Name { get; private set; }
    public string Horse { get; private set; }
    public Country? Country { get; private set; }
    public Club? Club { get; private set; }
    public Speed? MinAverageSpeed { get; private set; }
    public Speed? MaxAverageSpeed { get; private set; }
    public string Distance
    { 
        get => _distance.ToString("#.##");
        set => _distance = decimal.Parse(value);
    }

    public override string ToString()
    {
        var message = $"{"#".Localize()}{Number}: {Name}, {Horse}";
        var kmph = "km/h".Localize();
        if (MinAverageSpeed != null && MaxAverageSpeed != null)
        {
            return message + $" ({MinAverageSpeed}-{MaxAverageSpeed} {kmph})";
        }
        else if (MinAverageSpeed != null)
        {
            return message + $" ({"min".Localize()}:{MinAverageSpeed} {kmph})";
        }
        else
        {
            return message + $" ({"max".Localize()} : {MaxAverageSpeed}   {kmph})";
        }
    }
}
