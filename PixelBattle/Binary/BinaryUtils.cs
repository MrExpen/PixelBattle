using System.Diagnostics;
using System.IO.Hashing;
using PixelBattle.Binary;

namespace PixelBattle.Extensions;

public static class BinaryUtils
{
    public static T Read<T>(ReadOnlySpan<byte> buffer) where T : IBinaryReadable<T>
    {
        if (!T.TryRead(buffer, out var result))
        {
            throw new InvalidOperationException();
        }

        return result;
    }

    public static bool TryReadWithCrc32Validation<T>(ReadOnlySpan<byte> buffer, out T result)
        where T : IBinaryReadable<T>, IBinaryLength
    {
        if (!T.TryRead(buffer, out result))
        {
            return false;
        }

        Span<byte> crc32 = stackalloc byte[sizeof(uint)];
        if (!Crc32.TryHash(buffer[..T.BinaryLength], crc32, out var written))
        {
            return false;
        }

        Debug.Assert(written == sizeof(uint));
        return crc32.SequenceEqual(buffer.Slice(T.BinaryLength, sizeof(uint)));
    }

    public static T ReadWithCrc32Validation<T>(ReadOnlySpan<byte> buffer) where T : IBinaryReadable<T>, IBinaryLength
    {
        if (!TryReadWithCrc32Validation<T>(buffer, out var result))
        {
            throw new InvalidOperationException();
        }

        return result;
    }
}