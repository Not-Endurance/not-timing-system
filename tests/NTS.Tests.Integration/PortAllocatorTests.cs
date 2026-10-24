using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Every fixture finds a free port, closes it and starts its Functions host on it, and the host takes seconds to bind
/// it. A port can be taken by another fixture, a container or a browser in that time, and the host then dies with
/// "address already in use" and takes every test of its class with it. A start that fails that way is made again on
/// another port.
/// </summary>
public sealed class PortAllocatorTests
{
    const string TAKEN = "Failed to bind to address http://127.0.0.1:56950: address already in use.";

    [Fact]
    public async Task A_start_that_fails_because_the_port_was_taken_is_made_again_on_another_port()
    {
        var ports = new List<int>();

        var started = await PortAllocator.StartOnAFreePort(port =>
        {
            ports.Add(port);
            return ports.Count == 1
                ? throw new InvalidOperationException($"The host exited with code 1.{Environment.NewLine}{TAKEN}")
                : Task.FromResult(port);
        });

        Assert.Equal(2, ports.Count);
        Assert.Equal(ports[1], started);
    }

    [Fact]
    public async Task Any_other_failure_is_not_made_again()
    {
        var attempts = 0;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                PortAllocator.StartOnAFreePort<int>(_ =>
                {
                    attempts++;
                    throw new InvalidOperationException("The build output was not found.");
                })
        );

        Assert.Equal(1, attempts);
        Assert.Contains("build output", failure.Message);
    }

    [Fact]
    public async Task A_port_that_is_taken_again_and_again_fails_after_three_attempts()
    {
        var attempts = 0;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                PortAllocator.StartOnAFreePort<int>(_ =>
                {
                    attempts++;
                    throw new InvalidOperationException(TAKEN);
                })
        );

        Assert.Equal(3, attempts);
        Assert.Contains("address already in use", failure.Message);
    }
}
