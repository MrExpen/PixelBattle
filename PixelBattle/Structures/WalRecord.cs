using PixelBattle.Binary;

namespace PixelBattle.Structures;

public readonly struct WalRecord : IBinaryLength
{
    public static int BinaryLength => 17;

    public readonly long Timestamp;
    public readonly uint X;
    public readonly uint Y;
    public readonly byte Color;

    public WalRecord(long timestamp, uint x, uint y, byte color)
    {
        Timestamp = timestamp;
        X = x;
        Y = y;
        Color = color;
    }
}