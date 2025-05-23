using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct WalRecord : IBinaryLength, IBinaryWritable
{
    public static int BinaryLength => 25;

    public readonly long Timestamp;
    public readonly uint Version;
    public readonly uint Reserved;
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public WalRecord(long timestamp, uint version, uint reserved, int x, int y, byte color)
    {
        Timestamp = timestamp;
        Version = version;
        Reserved = reserved;
        X = x;
        Y = y;
        Color = color;
    }

    public WalRecord(long timestamp, uint version, int x, int y, byte color)
        : this(timestamp, version, 0, x, y, color)
    {
    }

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }
}