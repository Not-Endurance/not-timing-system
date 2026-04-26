namespace NTS.Application.Contracts.Shared;

public interface ISoftDeletableDocument
{
    bool IsDeleted { get; set; }
    int? DeletedVersion { get; set; }
}
