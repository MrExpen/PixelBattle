using PixelBattle.Binary;

namespace PixelBattle.Structures;

public readonly struct WalRecordHeaders : IBinaryLength, IBinaryWritable
{
    public static int BinaryLength => 16;

    public readonly long Timestamp;
    public readonly uint Version;
    public readonly OperationType Operation;

    public WalRecordHeaders(long timestamp, uint version, OperationType operation)
    {
        Timestamp = timestamp;
        Version = version;
        Operation = operation;
    }

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }
}