using System.Runtime.InteropServices;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
[StructLayout(LayoutKind.Explicit)]
public readonly struct DbHeaders : IBinarySerializable<DbHeaders>
{
    public static int BinaryLength => 28;

    public const int MagicNumberOffset = 0;
    public const int LastAppliedTimestampOffset = 8;
    public const int VersionOffset = 16;
    public const int WidthOffset = 20;
    public const int HeightOffset = 24;

    [FieldOffset(MagicNumberOffset)] 
    public readonly ulong MagicNumber;

    [FieldOffset(LastAppliedTimestampOffset)]
    public readonly long LastAppliedTimestamp;

    [FieldOffset(VersionOffset)] 
    public readonly uint Version;
    
    [FieldOffset(WidthOffset)] 
    public readonly int Width;
    
    [FieldOffset(HeightOffset)] 
    public readonly int Height;

    public DbHeaders(ulong magicNumber, long lastAppliedTimestamp, uint version, int width, int height)
    {
        MagicNumber = magicNumber;
        LastAppliedTimestamp = lastAppliedTimestamp;
        Version = version;
        Width = width;
        Height = height;
    }

    public static DbHeaders Read(ReadOnlySpan<byte> buffer) => Utils.Read<DbHeaders>(buffer, BinaryLength);

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }
}