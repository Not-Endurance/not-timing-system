using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using NTS.ACL.Entities.Athletes;
using NTS.ACL.Entities.Countries;
using NTS.ACL.Entities.EnduranceEvents;
using NTS.ACL.Entities.Horses;
using NTS.ACL.Entities.Participants;
using NTS.ACL.Entities.Participations;

namespace NTS.ACL;

public class EmsState
{
    public EmsEnduranceEvent Event { get; set; }
    public List<EmsHorse> Horses { get; private set; } = [];
    public List<EmsAthlete> Athletes { get; private set; } = [];
    public List<EmsParticipant> Participants { get; private set; } = [];
    public List<EmsParticipation> Participations { get; private set; } = [];

    [JsonIgnore]
    public IReadOnlyList<EmsCountry> Countries => new ReadOnlyCollection<EmsCountry>([new("BGN", "Bulgaria", 1337)]);

    public void Set(EmsState initial)
    {
        Event = initial.Event;
        Horses = initial.Horses;
        Athletes = initial.Athletes;
        Participants = initial.Participants;
        Participations = initial.Participations;
    }
}
