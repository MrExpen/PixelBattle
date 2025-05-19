namespace PixelBattle.Binary;

public interface IBinaryWritable
{
    public int Write(Span<byte> buffer);
}