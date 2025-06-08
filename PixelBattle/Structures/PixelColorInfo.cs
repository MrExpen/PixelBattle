namespace PixelBattle.Structures;

public readonly struct PixelColorInfo
{
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public PixelColorInfo(int x, int y, byte color)
    {
        X = x;
        Y = y;
        Color = color;
    }
}