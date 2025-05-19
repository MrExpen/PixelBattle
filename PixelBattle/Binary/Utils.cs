using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixelBattle.Binary;

public static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ReadOnlySpan<byte> buffer)
        where T : struct, IBinaryLength
    {
        if (buffer.Length < T.BinaryLength)
            throw new InvalidOperationException();

        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write<T>(T value, Span<byte> buffer)
        where T : struct, IBinaryLength
    {
        if (buffer.Length < T.BinaryLength)
            throw new InvalidOperationException();
        
        MemoryMarshal.Write(buffer, value);

        return T.BinaryLength;
    }
}