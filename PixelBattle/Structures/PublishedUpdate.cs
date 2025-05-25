namespace PixelBattle.Structures;

public readonly struct PublishedUpdate
{
    public readonly long Timestamp;
    public readonly long ChunkVersion;
    public readonly int X;
    public readonly int Y;
    public readonly byte Color;

    public PublishedUpdate(long timestamp, long chunkVersion, int x, int y, byte color)
    {
        Timestamp = timestamp;
        ChunkVersion = chunkVersion;
        X = x;
        Y = y;
        Color = color;
    }

    public override string ToString()
    {
        return
            $"{nameof(Timestamp)}: {Timestamp}, {nameof(ChunkVersion)}: {ChunkVersion}, {nameof(X)}: {X}, {nameof(Y)}: {Y}, {nameof(Color)}: {Color}";
    }
}