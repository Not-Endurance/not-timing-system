using Vup.reader;

namespace NTS.Judge.HardwareControllers;

public class VupVD67Controller : RfidController
{
    private const int NO_TAG_ERROR_CODE = 24;
    private readonly VD67Reader reader = null!;

    protected override string Device => "VD67";

    public VupVD67Controller(TimeSpan? throttle = null) : base(throttle)
    {
        this.reader = new VD67Reader();
    }

    public bool IsWaitingRead { get; private set; } 

    public override void Connect()
    {
        var connectionResult = this.reader.Connect();
        var setPowerResult = this.reader.SetAntPower(0, 27);
        if (!connectionResult.Success || !setPowerResult.Success)
        {
            this.RaiseError($"Connect failed '{connectionResult.ErrorCode}': '{connectionResult.Message}'");
            return;
        }
        this.RaiseMessage("Connected");
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

    public string Read()
    {
        if (!this.reader.IsConnected)
        {
            this.Connect();
        }
        this.IsWaitingRead = true;
        this.IsReading = false;
        this.IsWriting = false;
        while (this.IsWaitingRead && this.reader.IsConnected)
        {
            string data;
            var result = this.reader.Read6C(
                memory_bank.memory_bank_epc,
                TAG_WRITE_START_INDEX,
                TAG_DATA_LENGTH,
                Array.Empty<byte>(),
                Convert.FromHexString("00000000")); ;
            if (!result.Success)
            {
                if (result.ErrorCode != NO_TAG_ERROR_CODE)
                {
                    this.RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                }
            }
            else
            {
                data = this.ConvertToString(result.Result);
                this.RaiseMessage($"Read: '{data}'");
                this.IsWaitingRead = false;
                return data;
            }

            Thread.Sleep(throttle);
        }

        return null;
    }

    public IEnumerable<string> StartReading()
    {
        if (this.IsWaitingRead)
        {
            this.RaiseError("Cannot start continious reading while waiting on Read.");
            yield break;
        }
        if (!this.reader.IsConnected)
        {
            this.Connect();
        }
        this.IsReading = true;
        this.IsWriting = false;
        while (this.IsReading && this.reader.IsConnected)
        {
            string data;
            var result = this.reader.Read6C(
                memory_bank.memory_bank_epc,
                TAG_WRITE_START_INDEX,
                TAG_DATA_LENGTH,
                Array.Empty<byte>(),
                Convert.FromHexString("00000000")); ;
            if (!result.Success)
            {
                if (result.ErrorCode != NO_TAG_ERROR_CODE)
                {
                    this.RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                }
            }
            else
            {
                data = this.ConvertToString(result.Result);
                this.RaiseMessage($"Read: '{data}'");
                yield return data;
            }
            Thread.Sleep(throttle);
        }
    }

    public void StopReading()
    {
        this.IsReading = false;
    }

    public string Write(string data)
    {
        if (this.IsWaitingRead)
        {
            this.RaiseError("Cannot write while waiting on Read.");
            return null;
        }
        if (!this.reader.IsConnected)
        {
            this.Connect();
        }
        if (data.Length != TAG_DATA_LENGTH)
        {
            this.RaiseError($"Tag data length must be exactly {TAG_DATA_LENGTH} symbols");
            return null;
        }
        this.IsReading = false;
        this.IsWriting = true;
        while (this.IsWriting && this.reader.IsConnected)
        {
            try
            {
                var bytes = this.ConvertToByytes(data);
                var result = this.reader.Write6C(memory_bank.memory_bank_epc, 0, Array.Empty<byte>(), bytes, Convert.FromHexString("00000000"));
                if (!result.Success)
                {
                    if (result.ErrorCode != NO_TAG_ERROR_CODE)
                    {
                        this.RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                    }
                }
                else
                {
                    this.RaiseMessage($"Write Successful: '{data}'");
                    this.IsWriting = false;
                    return data;
                }
            }
            catch (Exception exception)
            {
                throw new Exception($"Write ERROR: {exception.Message + Environment.NewLine + exception.StackTrace}");
            }

            Thread.Sleep(this.throttle);
        }
        return null!;
    }
}
