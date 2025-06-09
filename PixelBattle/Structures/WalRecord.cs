using System.Buffers.Binary;
using PixelBattle.Binary;
using PixelBattle.Structures.Enums;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct WalRecord : IBinarySerializable<WalRecord>
{
    public static int BinaryLength => 30;

    private const int TimestampOffset = 0;
    private const int SequenceNumberOffset = 8;
    private const int SaltOffset = 16;
    private const int OperationOffset = 20;
    private const int XOffset = 21;
    private const int YOffset = 25;
    private const int ColorOffset = 29;

    public readonly long Timestamp;
    public readonly ulong SequenceNumber;
    public readonly int Salt;
    public readonly OperationType Operation;
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public WalRecord(
        long timestamp,
        ulong sequenceNumber,
        int salt,
        OperationType operation,
        int x = 0,
        int y = 0,
        byte color = 0
    )
    {
        Timestamp = timestamp;
        SequenceNumber = sequenceNumber;
        Salt = salt;
        Operation = operation;
        X = x;
        Y = y;
        Color = color;
    }

    public WalRecord(
        long timestamp,
        ulong sequenceNumber,
        int salt,
        OperationType operation,
        in PixelColorInfo color
    )
        : this(timestamp, sequenceNumber, salt, operation, color.X, color.Y, color.Color)
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
            BinaryPrimitives.ReadUInt64LittleEndian(buffer[SequenceNumberOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[SaltOffset..]),
            (OperationType)buffer[OperationOffset],
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
        BinaryPrimitives.WriteUInt64LittleEndian(buffer[SequenceNumberOffset..], SequenceNumber);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[SaltOffset..], Salt);
        buffer[OperationOffset] = (byte)Operation;
        BinaryPrimitives.WriteInt32LittleEndian(buffer[XOffset..], X);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[YOffset..], Y);
        buffer[ColorOffset] = Color;

        bytesWritten = BinaryLength;
        return true;
    }
}