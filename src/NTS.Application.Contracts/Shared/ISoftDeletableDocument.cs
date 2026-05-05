using Not.Structures;

namespace NTS.Application.Contracts.Shared;

// TODO: remove
public interface ISoftDeletableDocument : IIdentifiable
{
    bool IsDeleted { get; set; }
    int? DeletedVersion { get; set; }
}
