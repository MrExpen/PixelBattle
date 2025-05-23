using PixelBattle.Binary;

namespace PixelBattle.Structures;

[Serializable]
public readonly struct DbHeaders : IBinarySerializable<DbHeaders>
{
    public static int BinaryLength => 20;

    public readonly ulong MagicNumber;
    public readonly uint Version;
    public readonly int Width;
    public readonly int Height;

    public DbHeaders(ulong magicNumber, uint version, int width, int height)
    {
        MagicNumber = magicNumber;
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