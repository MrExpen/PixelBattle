using System.Runtime.InteropServices;
using PixelBattle.Binary;

namespace PixelBattle.Structures;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
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

    public static Headers Read(ReadOnlySpan<byte> buffer) => Utils.Read<Headers>(buffer);

    public int Write(Span<byte> buffer) => Utils.Write(this, buffer);
}