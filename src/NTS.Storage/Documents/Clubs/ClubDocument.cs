using Not.Random;
using NTS.Domain.Aggregates;

namespace NTS.Storage.Documents.Clubs;

public class ClubDocument : Document
{
    public ClubDocument(Club club)
        : base(club.Id)
    {
        Name = club.Name;
    }

    public string Name { get; init; }

    public Club ToDomain()
    {
        return new Club(Id, Name);
    }
}
