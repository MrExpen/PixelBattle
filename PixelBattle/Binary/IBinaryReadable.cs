namespace PixelBattle.Binary;

public interface IBinaryReadable<out T> where T : struct
{
    public static abstract T Read(ReadOnlySpan<byte> buffer);
}