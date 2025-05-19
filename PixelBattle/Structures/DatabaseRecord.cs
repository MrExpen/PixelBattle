using System.Numerics;
using System.Runtime.InteropServices;

namespace PixelBattle.Structures;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct DatabaseRecord
{
    public readonly long Timestamp;
    public readonly long UserId;
    public readonly byte Color;
}