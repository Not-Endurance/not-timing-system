﻿using NTS.Domain.Objects;

namespace NTS.Judge.Ports;

public interface IHubProcedures
{
    Task ReceiveSnapshot(Snapshot snapshot);
}
