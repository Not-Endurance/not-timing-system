using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using Athlete = NTS.Domain.Core.Aggregates.Participations.Athlete;
using Combination = NTS.Domain.Core.Aggregates.Participations.Combination;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Phase;
using Horse = NTS.Domain.Core.Aggregates.Participations.Horse;

namespace NTS.Witness;

public class DummyData
{
    public static Dictionary<string, string> CreateContacts()
    {
        var contacts = new Dictionary<string, string>
        {
            { "Yo mama", "+359 882312321" },
            { "Baba yaga", "+359 666666666" },
        };
        return contacts;
    }

    public static List<Participation> CreateParticipations(int number)
    {
        var participations = new List<Participation>();
        for (var i = 0; i < number; ++i)
        {
            var country = new Country(1000 + i, null, null, null, null);
            var names = new List<string> { $"FirstName{i + 1}", $"LastName{i + 1}" };
            var person = new Person(names.ToArray());

            var athlete = new Athlete(person, country, null, $"username{i + 1}");

            var horse = new Horse($"HorseName{i + 1}", null);

            var combination = new Combination(199 + i, i + 1, athlete, horse, null, (40 + i).ToString(), null, null);

            var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                i > 10 ? 30 : 20,
                15,
                i > 10 ? 60 : 40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                false,
                null,
                DateTimeOffset.Now.AddMinutes(23 + i)
            );

            var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(
                i > 10 ? 30 : 20,
                20,
                i > 10 ? 60 : 40,
                NTS.Domain.Enums.CompetitionRuleset.Regional,
                true,
                null,
                null
            );

            var phases = new List<NTS.Domain.Core.Aggregates.Participations.Phase> { phase1, phase2 };

            var phaseCollection = new PhaseCollection(phases);

            var competition = new Competition(
                $"CompetitionName{i}",
                Domain.Enums.CompetitionRuleset.Regional,
                Domain.Enums.CompetitionType.Qualification
            );

            var participation = new Participation(
                2001 + i,
                ParticipationCategory.Senior,
                competition,
                combination,
                phaseCollection,
                null
            );

            participations.Add(participation);
        }
        return participations;
    }

    public static Person CreateParticipant(string firstName, string secondName)
    {
        return new Person([firstName, secondName]);
    }

    public static List<CorePhase> CreatePhases()
    {
        var phases = new List<CorePhase>();
        var phase1 = new NTS.Domain.Core.Aggregates.Participations.Phase(
            20,
            15,
            40,
            NTS.Domain.Enums.CompetitionRuleset.Regional,
            false,
            null,
            DateTimeOffset.Now.AddMinutes(23)
        );
        var phase2 = new NTS.Domain.Core.Aggregates.Participations.Phase(
            20,
            20,
            40,
            NTS.Domain.Enums.CompetitionRuleset.Regional,
            true,
            null,
            null
        );
        phases.Add(phase1);
        phases.Add(phase2);
        return phases;
    }
}
