using System.Diagnostics;
using System.Text;
using Not.Logging;
using Vup.reader;

namespace NTS.Judge.RFID;

public class VupVF747pController : RfidController
{
    const int MAX_POWER = 27;

    readonly string _ipAddress;
    NetVupReader _reader;
    int _oneCount = 0;
    int _twoCount = 0;
    int _threeCount = 0;

    public VupVF747pController(string ipAddress, TimeSpan? throttle = null)
        : base(throttle)
    {
        _ipAddress = ipAddress;
        _reader = new NetVupReader(ipAddress, 1969, transport_protocol.tcp);
    }

    protected override string Device => "VF747p";

    public override void Connect()
    {
        var connectionResult = _reader.Connect();
        if (!connectionResult.Success)
        {
            var message = string.IsNullOrEmpty(connectionResult.Message)
                ? $"Unable to connect to VUP reader on IP '{_ipAddress}'."
                    + $" Make sure it's connected to the network and use 'Reconnect Hardware'"
                : connectionResult.Message;
            RaiseError(message);
            return;
        }
        IsConnected = true;
        SetPower(MAX_POWER);
        RaiseMessage($"Connected");
    }

    public void StartReading()
    {
        if (!IsConnected)
        {
            Connect();
        }
        var antennaIndices = GetAntennaIndices();
        IsReading = true;
        while (IsReading && _reader.IsConnected)
        {
            foreach (var tag in ReadTags(antennaIndices))
            {
                OnReadEvent((DateTime.Now, tag));
            }
            Thread.Sleep(Throttle);
        }
    }

    public void StopReading()
    {
        IsReading = false;
    }

    public override void Disconnect()
    {
        if (IsConnected)
        {
            _reader.Disconnect();
            IsConnected = false;
            RaiseMessage("Disconnected!");
        }
    }

    public void SetPower(int power)
    {
        var antennaIndices = GetAntennaIndices();
        foreach (var i in antennaIndices)
        {
            var setPowerResult = _reader.SetAntPower(i, power);
            if (!setPowerResult.Success)
            {
                var message = $"Error while setting antenna power: {setPowerResult.Message}";
                RaiseError(message);
            }
        }
    }

    int[] GetAntennaIndices()
    {
        var antennaRead = _reader.GetAntCount();
        if (!antennaRead.Success)
        {
            var message = $"Error in antenna read: {antennaRead.Message}";
            RaiseError(message);
            return [];
        }
        var indices = new List<int>();
        for (var i = 1; i <= antennaRead.Result; i++)
        {
            indices.Add(i);
        }
        return indices.ToArray();
    }

    IEnumerable<string> ReadTags(IEnumerable<int> antennaIndices)
    {
        var readTimer = new Stopwatch();
        var reconnectTimer = new Stopwatch();
        foreach (var i in antennaIndices)
        {
            _reader.SetWorkAnt(i);

            readTimer.Start();
            var password = Convert.FromHexString("00000000");
            var tagsBytes = _reader.List6C(
                memory_bank.memory_bank_epc,
                TAG_READ_START_INDEX,
                TAG_DATA_LENGTH,
                password
            );
            readTimer.Stop();

            if (readTimer.ElapsedMilliseconds is < 5)
            {
                reconnectTimer.Start();
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"VF747p reader response indicates a disconnect. Response time: '{readTimer.ElapsedMilliseconds}'"
                );
                var error = sb.ToString();
                RaiseError(error);
                Reconnect();
                reconnectTimer.Stop();
                sb.AppendLine($"Reconnected after '{reconnectTimer.ElapsedMilliseconds}'");
                var message = "VF747p-disconnected\n" + sb.ToString();
                LoggingHelper.Information(message);
            }

            if (tagsBytes.Success)
            {
                if (i == 1)
                {
                    _oneCount++;
                }
                if (i == 2)
                {
                    _twoCount++;
                }
                if (i == 3)
                {
                    _threeCount++;
                }

                var tags = tagsBytes.Result.Select(x => ConvertToString(x.Id));
                foreach (var tag in tags)
                {
                    Console.WriteLine(
                        $"Detected: {tag} antenna: {i} one:{_oneCount}, two:{_twoCount}, three :{_threeCount}"
                    );
                }
                return tags;
            }
        }
        return [];
    }

    void Reconnect()
    {
        Disconnect();
        _reader = new NetVupReader(_ipAddress, 1969, transport_protocol.tcp);
        var counter = 0;
        var before = DateTime.Now;
        do
        {
            Console.WriteLine($"Attempting to reconnect: {counter}");
            Connect();
            var timeout = TimeSpan.FromSeconds(1);
            Thread.Sleep(timeout);
            counter++;
        } while (!IsConnected);
        var after = DateTime.Now;
        Console.WriteLine($"Reconnected after '{counter}' attempts and '{(after - before).TotalSeconds}' seconds");
    }
}
