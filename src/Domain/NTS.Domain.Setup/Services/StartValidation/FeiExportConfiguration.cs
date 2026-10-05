using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Domain.Setup.Services.StartValidation;

/// <summary>
/// What the FEI export needs of a Setup (#628). An Event that has none of it configured is not exported, and is not asked
/// for any. Once any of it is, all of it is needed: the show ID, and for every competition that has any of its FEI values
/// all of them, with the FEI ID of every Horse and Athlete that rides in it.
/// </summary>
public static class FeiExportConfiguration
{
    public static IReadOnlyList<MissingFeiExportValue> MissingOf(ConfigureEvent setup)
    {
        var configured = setup.Competitions.Where(HasAnyOf).ToList();
        if (string.IsNullOrWhiteSpace(setup.FeiShowId) && configured.Count == 0)
        {
            return [];
        }

        var missing = new List<MissingFeiExportValue>();
        AddIfBlank(missing, setup.FeiShowId, FeiExportValue.ShowId, null);
        foreach (var competition in configured)
        {
            AddIfBlank(missing, competition.FeiEventId, FeiExportValue.EventId, competition.Name);
            AddIfBlank(missing, competition.FeiEventCode, FeiExportValue.EventCode, competition.Name);
            AddIfBlank(missing, competition.FeiCompetitionId, FeiExportValue.CompetitionId, competition.Name);
            AddIfBlank(missing, competition.FeiRule, FeiExportValue.Rule, competition.Name);
            AddIfBlank(missing, competition.FeiScheduleNumber, FeiExportValue.ScheduleNumber, competition.Name);
            foreach (var combination in competition.Participations.Select(x => x.Combination))
            {
                var horse = combination.Horse;
                var athlete = combination.Athlete;
                AddIfBlank(missing, horse.FeiId, FeiExportValue.FeiId, Rider(combination, horse.Name));
                AddIfBlank(missing, athlete.FeiId, FeiExportValue.FeiId, Rider(combination, athlete.Name));
            }
        }

        return missing;
    }

    static bool HasAnyOf(Competition competition)
    {
        return !string.IsNullOrWhiteSpace(competition.FeiEventId)
            || !string.IsNullOrWhiteSpace(competition.FeiEventCode)
            || !string.IsNullOrWhiteSpace(competition.FeiCompetitionId)
            || !string.IsNullOrWhiteSpace(competition.FeiRule)
            || !string.IsNullOrWhiteSpace(competition.FeiScheduleNumber);
    }

    static string Rider(Combination combination, string name)
    {
        return $"#{combination.Number}, {name}";
    }

    static void AddIfBlank(List<MissingFeiExportValue> missing, string? text, FeiExportValue value, string? subject)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            missing.Add(new MissingFeiExportValue(value, subject));
        }
    }
}
