using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Horses;

public interface IEmsHorseState : IEmsIdentifiable
{
    string FeiId { get; }
    string Name { get; }
    string Club { get; }
    bool IsStallion { get; }
    string Breed { get; }
    string TrainerFeiId { get; }
    string TrainerFirstName { get; }
    string TrainerLastName { get; }
}
