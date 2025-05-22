using PixelBattle.Binary;

namespace PixelBattle.Structures;

public readonly struct Headers : IBinarySerializable<Headers>
{
    public static int BinaryLength => 20;

    public readonly ulong MagicNumber;
    public readonly int Version;
    public readonly int Width;
    public readonly int Height;

    public Headers(ulong magicNumber, int version, int width, int height)
    {
        MagicNumber = magicNumber;
        Version = version;
        Width = width;
        Height = height;
    }

    public static Headers Read(ReadOnlySpan<byte> buffer) => Utils.Read<Headers>(buffer, BinaryLength);

    public int Write(Span<byte> buffer)
    {
        Utils.Write(this, buffer, BinaryLength);
        return BinaryLength;
    }
}