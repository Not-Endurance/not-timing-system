using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;

namespace NTS.Tools.Watcher;

public class WatcherTool
{
    private static WatcherMode _mode;
    private static string? _number;
    private static WatcherClient _client = default!;

    public static async Task Run()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  1. mode <mode> (arrive, vet, both)");
        Console.WriteLine("  2. #<tandem-number>");
        Console.WriteLine("  3. send <hh:mm:ss>");

        _client = await WatcherClient.Create();

        while (true)
        {
            var command = Prompt();
            await Process(command);
        }
    }

    static async Task SendParticipationEntry(SendCommand command)
    {
        if (_number == null)
        {
            Console.WriteLine("Select Tandem using '#<number>' command");
            return;
        }
        var participationEntry = CreatePayload(command.Time, _number);

        var type = _mode switch
        {
            WatcherMode.Arrive => WitnessEventType.Arrival,
            WatcherMode.Vet => WitnessEventType.VetIn,
            WatcherMode.Both => WitnessEventType.Arrival,
            _ => throw new Exception("Invalid mode"),
        };
        await _client.Send(participationEntry, type);

        if (_mode == WatcherMode.Both)
        {
            var inspectionTime = command.Time.AddMinutes(5);
            type = WitnessEventType.VetIn;
            participationEntry = CreatePayload(inspectionTime, _number);
            await _client.Send(participationEntry, type);
        }
    }

    static ParticipantEntry CreatePayload(DateTime date, string number)
    {
        return new ParticipantEntry
        {
            ArriveTime = date,
            Number = number,
            Name = "tools",
            LapDistance = 1337,
            LapNumber = 1337,
        };
    }

    static ICommand Prompt()
    {
        Console.Write("Command: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Invalid input!");
            return Prompt();
        }
        if (input.StartsWith("mode"))
        {
            return new ModeCommand(input.Split(" ").Last());
        }
        else if (input.StartsWith("#"))
        {
            return new NumberCommand(input.Replace("#", string.Empty));
        }
        else
        {
            return new SendCommand(input.Split(" ").Last());
        }
    }

    static async Task Process(ICommand command)
    {
        if (command is ModeCommand modeCommand)
        {
            _mode = modeCommand.Mode;
        }
        else if (command is NumberCommand numberCommand)
        {
            _number = numberCommand.Number;
        }
        else if (command is SendCommand sendCommand)
        {
            await SendParticipationEntry(sendCommand);
        }
    }
}

public record ModeCommand : ICommand
{
    public ModeCommand(string type)
    {
        Mode = Enum.Parse<WatcherMode>(type, true);
    }

    public WatcherMode Mode { get; }
}

public record NumberCommand : ICommand
{
    public NumberCommand(string number)
    {
        Number = number;
    }

    public string Number { get; }
}

public record SendCommand : ICommand
{
    public SendCommand(string timeString)
    {
        var segments = timeString.Split(":", StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
        var hour = segments.First();
        var minute = segments.Skip(1).FirstOrDefault();
        var second = segments.Skip(2).FirstOrDefault();
        Time = DateTime.Today.AddHours(hour).AddMinutes(minute).AddSeconds(second);
    }

    public DateTime Time { get; set; }
}

public interface ICommand { }

public enum WatcherMode
{
    Arrive = 1,
    Vet = 2,
    Both = 3,
}
