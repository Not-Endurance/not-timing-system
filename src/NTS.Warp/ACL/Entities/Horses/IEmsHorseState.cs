using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Horses;

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
