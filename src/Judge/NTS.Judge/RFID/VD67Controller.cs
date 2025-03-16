using Vup.reader;

namespace NTS.Judge.RFID;

public class VupVD67Controller : RfidController
{
    const int NO_TAG_ERROR_CODE = 24;

    readonly VD67Reader _reader = null!;

    public VupVD67Controller(TimeSpan? throttle = null)
        : base(throttle)
    {
        _reader = new VD67Reader();
    }

    protected override string Device => "VD67";
    public bool IsWaitingRead { get; private set; }

    public override void Connect()
    {
        var connectionResult = _reader.Connect();
        var setPowerResult = _reader.SetAntPower(0, 27);
        if (!connectionResult.Success || !setPowerResult.Success)
        {
            RaiseError($"Connect failed '{connectionResult.ErrorCode}': '{connectionResult.Message}'");
            return;
        }
        RaiseMessage("Connected");
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

    public string Read()
    {
        if (!_reader.IsConnected)
        {
            Connect();
        }
        IsWaitingRead = true;
        IsReading = false;
        IsWriting = false;
        while (IsWaitingRead && _reader.IsConnected)
        {
            string data;
            var epc = Array.Empty<byte>();
            var password = Convert.FromHexString("00000000");
            var result = _reader.Read6C(
                memory_bank.memory_bank_epc,
                TAG_WRITE_START_INDEX,
                TAG_DATA_LENGTH,
                epc,
                password
            );
            ;
            if (!result.Success)
            {
                if (result.ErrorCode != NO_TAG_ERROR_CODE)
                {
                    RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                }
            }
            else
            {
                data = ConvertToString(result.Result);
                RaiseMessage($"Read: '{data}'");
                IsWaitingRead = false;
                return data;
            }

            Thread.Sleep(Throttle);
        }

        return "";
    }

    public IEnumerable<string> StartReading()
    {
        if (IsWaitingRead)
        {
            RaiseError("Cannot start continious reading while waiting on Read.");
            yield break;
        }
        if (!_reader.IsConnected)
        {
            Connect();
        }
        IsReading = true;
        IsWriting = false;
        while (IsReading && _reader.IsConnected)
        {
            string data;
            var epc = Array.Empty<byte>();
            var password = Convert.FromHexString("00000000");
            var result = _reader.Read6C(
                memory_bank.memory_bank_epc,
                TAG_WRITE_START_INDEX,
                TAG_DATA_LENGTH,
                epc,
                password
            );
            ;
            if (!result.Success)
            {
                if (result.ErrorCode != NO_TAG_ERROR_CODE)
                {
                    RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                }
            }
            else
            {
                data = ConvertToString(result.Result);
                RaiseMessage($"Read: '{data}'");
                yield return data;
            }
            Thread.Sleep(Throttle);
        }
    }

    public void StopReading()
    {
        IsReading = false;
    }

    public string Write(string data)
    {
        if (IsWaitingRead)
        {
            RaiseError("Cannot write while waiting on Read.");
            return "";
        }
        if (!_reader.IsConnected)
        {
            Connect();
        }
        if (data.Length != TAG_DATA_LENGTH)
        {
            RaiseError($"Tag data length must be exactly {TAG_DATA_LENGTH} symbols");
            return "";
        }
        IsReading = false;
        IsWriting = true;
        while (IsWriting && _reader.IsConnected)
        {
            try
            {
                var bytes = ConvertToByytes(data);
                var epc = Array.Empty<byte>();
                var password = Convert.FromHexString("00000000");
                var result = _reader.Write6C(memory_bank.memory_bank_epc, 0, epc, bytes, password);
                if (!result.Success)
                {
                    if (result.ErrorCode != NO_TAG_ERROR_CODE)
                    {
                        RaiseError($"Read Failed '{result.ErrorCode}': {result.Message}");
                    }
                }
                else
                {
                    RaiseMessage($"Write Successful: '{data}'");
                    IsWriting = false;
                    return data;
                }
            }
            catch (Exception exception)
            {
                throw new Exception($"Write ERROR: {exception.Message + Environment.NewLine + exception.StackTrace}");
            }

            Thread.Sleep(Throttle);
        }
        return null!;
    }
}
