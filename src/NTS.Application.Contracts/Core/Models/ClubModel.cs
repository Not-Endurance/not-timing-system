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

public class ClubModel
{
    public static ClubModel MapFrom(Club club)
    {
        return new ClubModel { Id = club.Id, Name = club.Name };
    }

    public int Id { get; init; }
    public string TenantId { get; init; } = StorageConstants.DEFAULT_TENANT;
    public string Name { get; init; } = default!;

    public Club MapToEntity()
    {
        return new Club(Name, Id);
    }
}


