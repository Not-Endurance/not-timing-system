using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.Documents.Clubs;

public class ClubDocument : Document
{
    public static ClubDocument Create(IClub club)
    {
        return new ClubDocument
        {
            Id = club.Id,
            Name = club.Name,
        };
    }

    public string Name { get; init; } = default!;

    public Club ToDomain()
    {
        return new Club(Id, Name);
    }
}
