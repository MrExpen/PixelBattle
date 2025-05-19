using System.Runtime.InteropServices;
using PixelBattle.Structures;

namespace PixelBattle.Tests;

public class SerializationTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(ulong.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue)]
    [InlineData(ulong.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2)]
    [InlineData(52, 51, 50, 49)]
    public void HeadersRead_ShouldSuccess_ExactSize(ulong magicNumber, uint version, uint width, uint height)
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[Headers.BinaryLength];
        var expected = new Headers(magicNumber, version, width, height);
        MemoryMarshal.Write(buffer, magicNumber);
        MemoryMarshal.Write(buffer[8..], version);
        MemoryMarshal.Write(buffer[12..], width);
        MemoryMarshal.Write(buffer[16..], height);

        // Act
        var headers = Headers.Read(buffer);

        // Assert
        Assert.Equal(expected, headers);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(ulong.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, ulong.MaxValue)]
    [InlineData(ulong.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, ulong.MaxValue / 2)]
    [InlineData(52, 51, 50, 49, 48)]
    public void HeadersWrite_ShouldSuccess_ExactSize(
        ulong magicNumber,
        uint version,
        uint width,
        uint height,
        ulong trash
    )
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[Headers.BinaryLength + 8];
        MemoryMarshal.Write(buffer[Headers.BinaryLength..], trash);
        var headers = new Headers(magicNumber, version, width, height);

        // Act
        headers.Write(buffer);

        // Assert
        Assert.Equal(magicNumber, MemoryMarshal.Read<ulong>(buffer));
        Assert.Equal(version, MemoryMarshal.Read<uint>(buffer[8..]));
        Assert.Equal(width, MemoryMarshal.Read<uint>(buffer[12..]));
        Assert.Equal(height, MemoryMarshal.Read<uint>(buffer[16..]));
        Assert.Equal(trash, MemoryMarshal.Read<ulong>(buffer[Headers.BinaryLength..]));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(ulong.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, ulong.MaxValue)]
    [InlineData(ulong.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, uint.MaxValue / 2, ulong.MaxValue / 2)]
    [InlineData(52, 51, 50, 49, 48)]
    public void HeadersReadWrite_WithTrash(
        ulong magicNumber,
        uint version,
        uint width,
        uint height,
        ulong trash
    )
    {
        // Arrange
        Span<byte> buffer = stackalloc byte[Headers.BinaryLength + 8];
        MemoryMarshal.Write(buffer[Headers.BinaryLength..], trash);
        var headers = new Headers(magicNumber, version, width, height);

        // Act
        headers.Write(buffer);
        var read = Headers.Read(buffer);

        // Assert
        Assert.Equal(headers, read);
        Assert.Equal(trash, MemoryMarshal.Read<ulong>(buffer[Headers.BinaryLength..]));
    }
}