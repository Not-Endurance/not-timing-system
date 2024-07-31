﻿using Not.Injection;
using NTS.Domain.Core.Events;

namespace NTS.Judge.Ports;

public interface IClientProcedures : ITransientService
{
    Task SendStartCreated(PhaseCompleted phaseCompleted);
    Task SendQualificationRevoked(QualificationRevoked revoked);
    Task SendQualificationRestored(QualificationRestored restored);
}