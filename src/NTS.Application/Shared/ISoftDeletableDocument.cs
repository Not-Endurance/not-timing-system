namespace NTS.Application.Shared;

public interface ISoftDeletableDocument
{
    bool IsDeleted { get; set; }
    int? DeletedVersion { get; set; }
}
