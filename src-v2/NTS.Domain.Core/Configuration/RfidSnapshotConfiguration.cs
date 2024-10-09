using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTS.Domain.Core.Configuration;
public class RfidSnapshotConfiguration
{
    public RfidSnapshotConfiguration(SnapshotType snapshotType)
    {
        SnapshotType = snapshotType;
    }

    public SnapshotType SnapshotType { get; set; }
}
