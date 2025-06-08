using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct DbHeaders : IBinarySerializable<DbHeaders>
{
    public static readonly ulong DefaultMagicNumber = MemoryMarshal.Read<ulong>("PBDFEXPN"u8.ToArray());

    public static int BinaryLength => 32;

    private const int MagicNumberOffset = 0;
    private const int VersionOffset = 8;
    private const int LastAppliedWalIndexOffset = 12;
    private const int WidthOffset = 20;
    private const int HeightOffset = 24;
    private const int ChunkSizeOffset = 28;

    public readonly ulong MagicNumber;
    public readonly uint Version;
    public readonly long LastAppliedWalIndex;
    public readonly int Width;
    public readonly int Height;
    public readonly int ChunkSize;

    public DbHeaders(ulong magicNumber, uint version, long lastAppliedWalIndex, int width, int height, int chunkSize)
    {
        MagicNumber = magicNumber;
        Version = version;
        LastAppliedWalIndex = lastAppliedWalIndex;
        Width = width;
        Height = height;
        ChunkSize = chunkSize;
    }

    public static bool TryRead(ReadOnlySpan<byte> buffer, out DbHeaders result)
    {
        if (buffer.Length < BinaryLength)
        {
            result = default;
            return false;
        }

        result = new DbHeaders(
            BinaryPrimitives.ReadUInt64LittleEndian(buffer[MagicNumberOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[VersionOffset..]),
            BinaryPrimitives.ReadInt64LittleEndian(buffer[LastAppliedWalIndexOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[WidthOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[HeightOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[ChunkSizeOffset..])
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

        BinaryPrimitives.WriteUInt64LittleEndian(buffer[MagicNumberOffset..], MagicNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[VersionOffset..], Version);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[LastAppliedWalIndexOffset..], LastAppliedWalIndex);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[WidthOffset..], Width);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[HeightOffset..], Height);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[ChunkSizeOffset..], ChunkSize);

        bytesWritten = BinaryLength;
        return true;
    }
}