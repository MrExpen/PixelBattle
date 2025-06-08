namespace PixelBattle.Binary;

public interface IBinaryWritable
{
    public bool TryWrite(Span<byte> buffer, out int bytesWritten);
}