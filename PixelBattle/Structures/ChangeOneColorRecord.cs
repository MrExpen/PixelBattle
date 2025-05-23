using PixelBattle.Binary;

namespace PixelBattle.Structures;

public readonly struct ChangeOneColorRecord : IBinaryLength, IBinaryWritable
{
    public static int BinaryLength => 9;
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public ChangeOneColorRecord(int x, int y, byte color)
    {
        X = x;
        Y = y;
        Color = color;
    }

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }
}