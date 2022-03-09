using EnduranceJudge.Domain.Core.Models;
using EnduranceJudge.Domain.State.Athletes;
using EnduranceJudge.Domain.State.Horses;
using EnduranceJudge.Domain.State.TimeRecords;
using System.Collections.Generic;
using System.Linq;

namespace EnduranceJudge.Domain.State.Participants
{
    public class Participant : DomainBase<ParticipantException>, IParticipantState
    {
        public const int DEFAULT_MAX_AVERAGE_SPEED = 16;
        private const string NAME_FORMAT = "{0} - {1} with {2}";

        private List<TimeRecord> timeRecords = new();

        private Participant() {}
        public Participant(Athlete athlete, Horse horse) : base(GENERATE_ID)
        {
            this.Horse = horse;
            this.Athlete = athlete;
        }
        public Participant(Athlete athlete, Horse horse, IParticipantState state) : base(GENERATE_ID)
        {
            this.RfId = state.RfId;
            this.MaxAverageSpeedInKmPh = state.MaxAverageSpeedInKmPh;
            this.Number = state.Number;
            this.Athlete = athlete;
            this.Horse = horse;
        }

        public string RfId { get; internal set; }
        public int Number { get; internal set; }
        public int? MaxAverageSpeedInKmPh { get; internal set; }
        public Horse Horse { get; internal set; }
        public Athlete Athlete { get; internal set; }
        public IReadOnlyList<TimeRecord> TimeRecords
        {
            get => this.timeRecords.AsReadOnly();
            private set => this.timeRecords = value.ToList();
        }

        public void Add(TimeRecord record)
            => this.timeRecords.Add(record);

        public string Name => FormatName(this.Number, this.Athlete.Name, this.Horse.Name);

        public static string FormatName(int number, string athleteName, string horseName)
        {
            return string.Format(NAME_FORMAT, number, athleteName, horseName);
        }
    }
}
