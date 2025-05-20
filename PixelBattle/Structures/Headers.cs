using PixelBattle.Binary;

namespace PixelBattle.Structures;

public readonly struct Headers : IBinarySerializable<Headers>
{
    public static int BinaryLength => 20;

    public readonly ulong MagicNumber;
    public readonly uint Version;
    public readonly uint Width;
    public readonly uint Height;

    public Headers(ulong magicNumber, uint version, uint width, uint height)
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