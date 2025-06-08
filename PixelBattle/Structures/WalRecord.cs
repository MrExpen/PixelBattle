using System.Buffers.Binary;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct WalRecord : IBinarySerializable<WalRecord>
{
    public static int BinaryLength => 21;

    private const int TimestampOffset = 0;
    private const int SaltOffset = 8;
    private const int XOffset = 12;
    private const int YOffset = 16;
    private const int ColorOffset = 20;

    public readonly long Timestamp;
    public readonly int Salt;
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public WalRecord(long timestamp, int salt, int x, int y, byte color)
    {
        Timestamp = timestamp;
        Salt = salt;
        X = x;
        Y = y;
        Color = color;
    }

    public WalRecord(long timestamp, int salt, in PixelColorInfo color)
        : this(timestamp, salt, color.X, color.Y, color.Color)
    {
    }

    public static bool TryRead(ReadOnlySpan<byte> buffer, out WalRecord result)
    {
        if (buffer.Length < BinaryLength)
        {
            result = default;
            return false;
        }

        result = new WalRecord(
            BinaryPrimitives.ReadInt64LittleEndian(buffer[TimestampOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[SaltOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[XOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[YOffset..]),
            buffer[ColorOffset]
        );
        return true;
    }

    public bool TryWrite(Span<byte> buffer, out int bytesWritten)
    {
        if (buffer.Length < BinaryLength)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteInt64LittleEndian(buffer[TimestampOffset..], Timestamp);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[SaltOffset..], Salt);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[XOffset..], X);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[YOffset..], Y);
        buffer[ColorOffset] = Color;

        bytesWritten = BinaryLength;
        return true;
    }
}