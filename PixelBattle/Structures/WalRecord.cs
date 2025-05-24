using System.Runtime.InteropServices;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
[StructLayout(LayoutKind.Explicit)]
public readonly struct WalRecord : IBinaryLength, IBinaryWritable, IBinaryReadable<WalRecord>
{
    public static int BinaryLength => 21;

    [FieldOffset(0)] public readonly long Timestamp;
    [FieldOffset(8)] public readonly uint Version;
    [FieldOffset(12)] public readonly int X;
    [FieldOffset(16)] public readonly int Y;
    [FieldOffset(20)] public readonly byte Color;

    public WalRecord(long timestamp, uint version, int x, int y, byte color)
    {
        Timestamp = timestamp;
        Version = version;
        X = x;
        Y = y;
        Color = color;
    }

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }

    public static WalRecord Read(ReadOnlySpan<byte> buffer) => Utils.Read<WalRecord>(buffer, BinaryLength);
}