using EMS.Judge.Application.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Vup.reader;

namespace EMS.Judge.Application.Hardware;

public class VupVF747pController : RfidController
{ 
    private const int MAX_POWER = 27;
    private NetVupReader reader;
    private readonly string ipAddress;
	private readonly ILogger logger;

    protected override string Device => "VF747p";

    public VupVF747pController(string ipAddress, ILogger logger, TimeSpan? throttle = null) : base(throttle)
    {
        this.ipAddress = ipAddress;
		this.logger = logger;
		this.reader = new NetVupReader(ipAddress, 1969, transport_protocol.tcp);
    }

    public override void Connect()
    {
        var connectionResult = this.reader.Connect();
        if (!connectionResult.Success)
        {
            var message = string.IsNullOrEmpty(connectionResult.Message)
                ? $"Unable to connect to VUP reader on IP '{this.ipAddress}'." +
                    $" Make sure it's connected to the network and use 'Reconnect Hardware'"
                : connectionResult.Message;
            this.RaiseError(message);
            return;
        }
        this.IsConnected = true;
        this.SetPower(MAX_POWER);
        this.RaiseMessage($"Connected");
    }

    public IEnumerable<string> StartReading()
    {
        if (!this.IsConnected)
        {
            this.Connect();
        }
        var antennaIndices = this.GetAntennaIndices();
        this.IsReading = true;
        while (this.IsReading && this.reader.IsConnected)
        {
            foreach (var tag in this.ReadTags(antennaIndices))
            {
                yield return tag;
            }
            Thread.Sleep(this.throttle);
        }
    }

    public void StopReading()
    {
        this.IsReading = false;
    }

    public override void Disconnect()
    {
        if (this.IsConnected)
        {
            this.reader.Disconnect();
            this.IsConnected = false;
            this.RaiseMessage("Disconnected!");
        }
    }

    public void SetPower(int power)
    {
        var antennaIndices = this.GetAntennaIndices();
        foreach (var i in antennaIndices)
        {
            var setPowerResult = this.reader.SetAntPower(i, power);
            if (!setPowerResult.Success)
            {
                var message = $"Error while setting antenna power: {setPowerResult.Message}";
                this.RaiseError(message);
            }
        }
    }

    private int[] GetAntennaIndices()
    {
        var antennaRead = this.reader.GetAntCount();
        if (!antennaRead.Success)
        {
            var message = $"Error in antenna read: {antennaRead.Message}";
            this.RaiseError(message);
            return Array.Empty<int>();
        }
        var indices = new List<int>();
        for (var i = 1; i <= antennaRead.Result; i++)
        {
            indices.Add(i);
        }
        return indices.ToArray();
    }

    private IEnumerable<string> ReadTags(IEnumerable<int> antennaIndices)
    {
        var readTimer = new Stopwatch();
        var reconnectTimer = new Stopwatch();
        foreach (var i in antennaIndices)
        {
            this.reader.SetWorkAnt(i);
            
            readTimer.Start();
            var tagsBytes = this.reader.List6C(
                memory_bank.memory_bank_epc,
                TAG_READ_START_INDEX,
                TAG_DATA_LENGTH,
                Convert.FromHexString("00000000"));
            readTimer.Stop();
            
            if (readTimer.ElapsedMilliseconds is < 5)
            {
                reconnectTimer.Start();
                var sb = new StringBuilder();
                sb.AppendLine($"VF747p reader response indicates a disconnect. Response time: '{readTimer.ElapsedMilliseconds}'");
				this.RaiseError(sb.ToString());
                this.Reconnect();
                reconnectTimer.Stop();
                sb.AppendLine($"Reconnected after '{reconnectTimer.ElapsedMilliseconds}'");
                this.logger.Log("VF747p-disconnected", sb.ToString());
            }

            if (tagsBytes.Success)
            {
				var tags = tagsBytes.Result.Select(x => this.ConvertToString(x.Id));
                foreach (var tag in tags)
                {
					Console.WriteLine($"Detected: {tag}");
				}
				return tags;
            }
        }
        return Enumerable.Empty<string>();
    }

    private void Reconnect()
    {
        this.Disconnect();
		this.reader = new NetVupReader(ipAddress, 1969, transport_protocol.tcp);
		var counter = 0;
        var before = DateTime.Now;
        do
        {
            Console.WriteLine($"Attempting to reconnect: {counter}");
            this.Connect();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            counter++;
        }
        while (!this.IsConnected);
        var after = DateTime.Now;
        Console.WriteLine($"Reconnected after '{counter}' attempts and '{(after - before).TotalSeconds}' seconds");
    }
}
