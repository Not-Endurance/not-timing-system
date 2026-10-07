using System.Reflection;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Ui;
using NTS.Application;
using NTS.Contracts;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Tests.Integration;

/// <summary>
/// What the server that records every time made unnecessary is gone and stays gone (#644, ADR-0013): the Judge hub with its
/// procedures and relay, the in-memory map of who the primary connection is, the store of Snapshots that waited for a Judge,
/// the hub's own write path with its bearer and scope checks, the receive authorizer and the UDP handshake. A person's
/// Snapshot is an authenticated request, so nothing on the hub is a write and nothing is queued for a connection that is not
/// there. The tests look for them by name in what the hosts are made of, and in the collections an Event keeps, so that one
/// that comes back is noticed.
/// </summary>
public sealed class RemovedPartsTests
{
    static readonly string[] NAMES_OF_WHAT_IS_GONE =
    [
        "PendingSnapshot",
        "PrimaryConnection",
        "Handshake",
        "NetworkBroadcast",
        "ReceiveAuthoriz",
        "JudgeHub",
        "JudgeConnection",
        "JudgeRelay",
        "JudgeProcedure",
        "RelayToJudge",
    ];
    static readonly Assembly[] WHAT_THE_HOSTS_ARE_MADE_OF =
    [
        typeof(LiveHub).Assembly,
        typeof(MongoConstants).Assembly,
        typeof(Routes).Assembly,
        typeof(NtsApplicationServices).Assembly,
        typeof(ApplicationConstants).Assembly,
        typeof(Not.Application.NApplicationBuilder).Assembly,
    ];

    [Fact]
    public void No_type_of_the_hosts_is_one_of_the_parts_the_server_that_records_made_unnecessary()
    {
        var found = WHAT_THE_HOSTS_ARE_MADE_OF
            .SelectMany(x => x.GetTypes())
            .Where(type => NAMES_OF_WHAT_IS_GONE.Any(name => type.FullName!.Contains(name, StringComparison.Ordinal)))
            .Select(x => x.FullName)
            .ToArray();

        Assert.Empty(found);
    }

    [Fact]
    public void No_collection_of_the_Api_or_of_the_Functions_host_holds_Snapshots_that_wait_for_a_Judge()
    {
        var collections = typeof(MongoConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral)
            .Select(x => (string)x.GetRawConstantValue()!)
            .Concat(TenantOwned.Collections)
            .ToArray();

        Assert.DoesNotContain(collections, x => x.Contains("pending", StringComparison.OrdinalIgnoreCase));
    }
}
