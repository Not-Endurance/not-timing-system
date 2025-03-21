namespace NTS.Relay.ACL.Entities.Participants;

public class EmsRfidTag : IEquatable<EmsRfidTag>
{
    public const char EMPTY_CHAR = '0';

    public EmsRfidTag() { }

    public EmsRfidTag(string tagData)
    {
        var number = tagData.Substring(0, 3);
        var position = tagData.Substring(3, 6);
        var id = tagData.Substring(9);
        Id = id;
        Position = position;
        ParticipantNumber = int.Parse(number).ToString();
    }

    public string Id { get; set; }
    public string Position { get; set; }
    public string ParticipantNumber { get; set; }

    public bool Equals(EmsRfidTag other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        if (obj is EmsRfidTag tag)
        {
            return Equals(tag);
        }
        return false;
    }

    public override string ToString()
    {
        var number = ParticipantNumber.PadLeft(3, EMPTY_CHAR);
        var position = Position.PadLeft(6, EMPTY_CHAR);
        return number + position + Id;
    }
}
