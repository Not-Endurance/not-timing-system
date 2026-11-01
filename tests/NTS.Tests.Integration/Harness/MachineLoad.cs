namespace NTS.Tests.Integration.Harness;

/// <summary>
/// What the machine is doing, read from /proc on Linux: the share of CPU time that was busy since the previous reading, and
/// the memory a new process can get. Where /proc is not there, <see cref="IsAvailable"/> is false and nothing is read.
/// </summary>
internal sealed class MachineLoad
{
    long _busy;
    long _total;

    public static bool IsAvailable => File.Exists("/proc/stat") && File.Exists("/proc/meminfo");

    /// <summary>The busy share of the CPU time since the previous reading, from 0 to 1. The first reading is 0.</summary>
    public double ReadBusyShare()
    {
        // The columns after "cpu" are user, nice, system, idle, iowait, irq, softirq and steal.
        var columns = File.ReadLines("/proc/stat")
            .First()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(long.Parse)
            .ToArray();
        var busy = columns[0] + columns[1] + columns[2] + columns[5] + columns[6] + columns[7];
        var total = busy + columns[3] + columns[4];
        var share = total > _total ? (double)(busy - _busy) / (total - _total) : 0;
        _busy = busy;
        _total = total;
        return share;
    }

    /// <summary>The memory a new process can get, in kilobytes, as the kernel estimates it.</summary>
    public static long AvailableMemoryKb()
    {
        var line = File.ReadLines("/proc/meminfo").First(x => x.StartsWith("MemAvailable:", StringComparison.Ordinal));
        return long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
    }
}
