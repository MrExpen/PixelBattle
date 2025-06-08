using System.IO.Hashing;
using PixelBattle.Binary;

namespace PixelBattle.Extensions;

public static class BinaryWritableExtensions
{
    public static int Write<T>(this T @this, Span<byte> buffer) where T : IBinaryWritable
    {
        if (!@this.TryWrite(buffer, out var written))
        {
            throw new InvalidOperationException();
        }

        return written;
    }

    public static bool TryWriteWithCrc32<T>(this T @this, Span<byte> buffer, out int written)
        where T : IBinaryWritable
    {
        if (!@this.TryWrite(buffer, out var structureLength) ||
            buffer.Length < structureLength + sizeof(uint) ||
            !Crc32.TryHash(buffer[..structureLength], buffer[structureLength..], out var bytesWritten)
           )
        {
            written = 0;
            return false;
        }

        written = structureLength + bytesWritten;
        return true;
    }

    public static int WriteWithCrc32<T>(this T @this, Span<byte> buffer) where T : IBinaryWritable
    {
        if (!@this.TryWriteWithCrc32(buffer, out var written))
        {
            throw new InvalidOperationException();
        }

        return written;
    }
}