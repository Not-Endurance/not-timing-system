using NTS.ACL.RPC.Procedures;

namespace NTS.Application.RPC;

public interface ILegacyWitnessClientProcedures
    : IEmsParticipantsClientProcedures,
        IEmsParticipantsHubProcedures,
        IEmsStartlistClientProcedures { }
