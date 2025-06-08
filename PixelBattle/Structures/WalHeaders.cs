using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct WalHeaders : IBinarySerializable<WalHeaders>
{
    public static readonly ulong DefaultMagicNumber = MemoryMarshal.Read<ulong>("PBWFEXPN"u8.ToArray());
    public static int BinaryLength => 16;

    private const int MagicNumberOffset = 0;
    private const int VersionOffset = 8;
    private const int SaltOffset = 12;

    public readonly ulong MagicNumber;
    public readonly uint Version;
    public readonly int Salt;

    public WalHeaders(ulong magicNumber, uint version, int salt)
    {
        MagicNumber = magicNumber;
        Version = version;
        Salt = salt;
    }

    public static bool TryRead(ReadOnlySpan<byte> buffer, out WalHeaders result)
    {
        if (buffer.Length < BinaryLength)
        {
            result = default;
            return false;
        }

        result = new WalHeaders(
            BinaryPrimitives.ReadUInt64LittleEndian(buffer[MagicNumberOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer[VersionOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(buffer[SaltOffset..])
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
        BinaryPrimitives.WriteInt32LittleEndian(buffer[SaltOffset..], Salt);

        bytesWritten = BinaryLength;
        return true;
    }
}