using NTS.Relay.ACL.RPC.Procedures;

namespace NTS.Relay.RPC.Procedures;

public interface ILegacyWitnessClientProcedures
    : IEmsParticipantsClientProcedures,
        IEmsParticipantsHubProcedures,
        IEmsStartlistClientProcedures { }
