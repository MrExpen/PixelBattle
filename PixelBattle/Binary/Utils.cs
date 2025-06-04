using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixelBattle.Binary;

public static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ReadOnlySpan<byte> buffer) where T : struct, IBinaryLength
    {
        return Read<T>(buffer, T.BinaryLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ReadOnlySpan<byte> buffer, int size) where T : struct
    {
        T result = default;
        Read(buffer, ref result, size);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Read<T>(ReadOnlySpan<byte> source, ref T result, int size) where T : struct
    {
        if (source.Length < size)
            throw new InvalidOperationException();

        var destination = AsSpan(ref result, size);
        source[..size].CopyTo(destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write<T>(T value, Span<byte> buffer) where T : struct, IBinaryLength
    {
        var size = T.BinaryLength;

        Write(buffer, ref value, size);

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(T value, Span<byte> buffer, int size) where T : struct
    {
        Write(buffer, ref value, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(Span<byte> destination, ref T value, int size) where T : struct
    {
        if (destination.Length < size)
            throw new InvalidOperationException();

        var source = AsSpan(ref value, size);
        source.CopyTo(destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsSpan<T>(ref T value, int size) where T : struct
    {
        return MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsSpan<T>(ref T value) where T : struct, IBinaryLength
    {
        return AsSpan(ref value, T.BinaryLength);
    }
}