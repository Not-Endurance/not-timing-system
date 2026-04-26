using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;


namespace NTS.Application.Contracts.Core.Models;

public class CoreSnapshotModel
{
    public static CoreSnapshotModel MapFrom(Snapshot snapshot)
    {
        return new CoreSnapshotModel
        {
            Number = snapshot.Number,
            Type = snapshot.Type,
            Method = snapshot.Method,
            RecordedAt = snapshot.Timestamp.ToDateTimeOffset(),
        };
    }

    public int Number { get; set; }
    public SnapshotType Type { get; set; }
    public SnapshotMethod Method { get; set; }
    public DateTimeOffset RecordedAt { get; set; }

    public Snapshot MapToEntity()
    {
        return new Snapshot(Number, Type, Method, new Timestamp(RecordedAt));
    }
}


