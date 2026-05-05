using Not.Structures;

namespace NTS.Application.Contracts.Shared;

public interface ISoftDeletableDocument : IIdentifiable
{
    bool IsDeleted { get; set; }
    int? DeletedVersion { get; set; }
}
