using Not.Random;
using NTS.Domain.Objects;

namespace NTS.Storage.Documents.Clubs;

public class ClubDocument : Document
{
    public ClubDocument(Club club)
        : base(RandomHelper.GenerateUniqueInteger()) // TODO: convert Club to entity
    {
        Name = club.Name;
    }

    public string Name { get; init; }
}
