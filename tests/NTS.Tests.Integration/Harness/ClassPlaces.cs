namespace NTS.Tests.Integration.Harness;

/// <summary>
/// The classes that run at once (ADR-0016). Four to start, and then the number follows the machine: one more while its CPUs
/// have room and a class waits for a place, one fewer only while they are saturated or the memory left is short. It never
/// goes below four, which the suite was verified at. It does not stop a class that runs, only the next one from starting.
/// Where the machine does not report its load, it stays at four.
/// </summary>
internal static class ClassPlaces
{
    const int START = 4;
    const int FLOOR = 4;
    const int CEILING = 8;
    const double IDLE_BELOW = 0.92;
    const double SATURATED_ABOVE = 0.99;
    const long MEMORY_FLOOR_KB = 1024 * 1024;
    const int STEADY_READINGS = 2;

    static readonly object LOCK = new();
    static readonly Queue<TaskCompletionSource> WAITING = new();
    static readonly MachineLoad LOAD = new();
    static readonly string? LOG = Environment.GetEnvironmentVariable("NTS_CLASS_PLACES_LOG");
    static bool _started;
    static Timer? _sampler;
    static int _limit = START;
    static int _running;
    static int _idleReadings;
    static int _busyReadings;

    public static Task WaitAsync()
    {
        lock (LOCK)
        {
            if (!_started)
            {
                _started = true;
                if (MachineLoad.IsAvailable)
                {
                    _sampler = new Timer(_ => Sample(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                }
            }

            if (_running < _limit)
            {
                _running++;
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            WAITING.Enqueue(waiter);
            return waiter.Task;
        }
    }

    public static void Release()
    {
        lock (LOCK)
        {
            _running--;
            Admit();
        }
    }

    static void Admit()
    {
        while (WAITING.Count > 0 && _running < _limit)
        {
            _running++;
            WAITING.Dequeue().SetResult();
        }
    }

    /// <summary>One reading a second: the number moves only after the same reading has come more than once in a row.</summary>
    static void Sample()
    {
        lock (LOCK)
        {
            var busy = LOAD.ReadBusyShare();
            var availableKb = MachineLoad.AvailableMemoryKb();
            if (availableKb < MEMORY_FLOOR_KB || busy > SATURATED_ABOVE)
            {
                _busyReadings++;
                _idleReadings = 0;
            }
            else if (busy < IDLE_BELOW && WAITING.Count > 0)
            {
                _idleReadings++;
                _busyReadings = 0;
            }
            else
            {
                _busyReadings = 0;
                _idleReadings = 0;
            }

            if (_busyReadings >= STEADY_READINGS && _limit > FLOOR)
            {
                _limit--;
                _busyReadings = 0;
            }
            else if (_idleReadings >= STEADY_READINGS && _limit < CEILING)
            {
                _limit++;
                _idleReadings = 0;
                Admit();
            }

            if (LOG != null)
            {
                File.AppendAllText(
                    LOG,
                    $"{DateTime.UtcNow:HH:mm:ss} busy {busy:P0} free {availableKb / 1024} MB limit {_limit} running {_running} waiting {WAITING.Count}{Environment.NewLine}"
                );
            }
        }
    }
}
