namespace PixelBattle.Structures;

public readonly struct UpdateColor
{
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public UpdateColor(int x, int y, byte color)
    {
        X = x;
        Y = y;
        Color = color;
    }
}