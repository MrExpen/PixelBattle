namespace PixelBattle.Binary;

public interface IBinaryReadable<T> where T : allows ref struct
{
    public static abstract bool TryRead(ReadOnlySpan<byte> buffer, out T result);
}