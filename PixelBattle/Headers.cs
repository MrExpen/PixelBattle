using System.Runtime.InteropServices;

namespace PixelBattle;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Headers
{
    public readonly ulong MagicNumber;
    public readonly uint Version;
    public readonly uint Width;
    public readonly uint Height;
    public readonly byte Color;

    public Headers(ulong magicNumber, uint version, uint width, uint height, byte color)
    {
        MagicNumber = magicNumber;
        Version = version;
        Width = width;
        Height = height;
        Color = color;
    }
}