using System.Reflection;
using System.Security.Cryptography;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.Tests;

public class SerializationTests
{
    public static TheoryData<Type> BinaryLengthTypes =>
        new(typeof(DbHeaders).Assembly.GetTypes()
            .Where(x => x.IsAssignableTo(typeof(IBinaryLength)))
            .Where(x => x is { IsAbstract: false, IsInterface: false })
        );

    [Theory]
    [MemberData(nameof(BinaryLengthTypes))]
    public void GenericReadWriteTest(Type type)
    {
        GetType().GetRuntimeMethods().Single(x => x.Name == nameof(InternalGenericReadWriteTest))
            .MakeGenericMethod([type])
            .Invoke(this, []);
    }

    private void InternalGenericReadWriteTest<T>() where T : struct, IBinarySerializable<T>
    {
        // Arrange
        Span<byte> expected = stackalloc byte[T.BinaryLength + 8];
        RandomNumberGenerator.Fill(expected);
        Span<byte> copy = stackalloc byte[expected.Length];
        expected.CopyTo(copy);

        // Act
        var readSuccess = T.TryRead(copy, out var result);
        copy[..T.BinaryLength].Clear();
        var writeSuccess = result.TryWrite(copy, out var written);

        // Assert
        Assert.True(readSuccess);
        Assert.True(writeSuccess);
        Assert.Equal(T.BinaryLength, written);
        Assert.Equal(expected, copy);
    }


    // [Theory]
    // [InlineData(0, 0, 0, 0)]
    // [InlineData(ulong.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
    // [InlineData(ulong.MaxValue / 2, int.MaxValue / 2, int.MaxValue / 2, int.MaxValue / 2)]
    // [InlineData(52, 51, 50, 49)]
    // public void HeadersRead_ShouldSuccess_ExactSize(ulong magicNumber, uint version, int width, int height)
    // {
    //     // Arrange
    //     Span<byte> buffer = stackalloc byte[DbHeaders.BinaryLength];
    //     var expected = new DbHeaders(magicNumber, version, width, height);
    //     MemoryMarshal.Write(buffer, magicNumber);
    //     MemoryMarshal.Write(buffer[8..], version);
    //     MemoryMarshal.Write(buffer[12..], width);
    //     MemoryMarshal.Write(buffer[16..], height);
    //
    //     // Act
    //     var headers = DbHeaders.Read(buffer);
    //
    //     // Assert
    //     Assert.Equal(expected, headers);
    // }
    //
    // [Theory]
    // [InlineData(0, 0, 0, 0, 0)]
    // [InlineData(ulong.MaxValue, uint.MaxValue, int.MaxValue, int.MaxValue, ulong.MaxValue)]
    // [InlineData(ulong.MaxValue / 2, uint.MaxValue / 2, int.MaxValue / 2, int.MaxValue / 2, ulong.MaxValue / 2)]
    // [InlineData(52, 51, 50, 49, 48)]
    // public void HeadersWrite_ShouldSuccess_ExactSize(
    //     ulong magicNumber,
    //     uint version,
    //     int width,
    //     int height,
    //     ulong trash
    // )
    // {
    //     // Arrange
    //     Span<byte> buffer = stackalloc byte[DbHeaders.BinaryLength + 8];
    //     MemoryMarshal.Write(buffer[DbHeaders.BinaryLength..], trash);
    //     var headers = new DbHeaders(magicNumber, version, width, height);
    //
    //     // Act
    //     headers.Write(buffer);
    //
    //     // Assert
    //     Assert.Equal(magicNumber, MemoryMarshal.Read<ulong>(buffer));
    //     Assert.Equal(version, MemoryMarshal.Read<uint>(buffer[8..]));
    //     Assert.Equal(width, MemoryMarshal.Read<int>(buffer[12..]));
    //     Assert.Equal(height, MemoryMarshal.Read<int>(buffer[16..]));
    //     Assert.Equal(trash, MemoryMarshal.Read<ulong>(buffer[DbHeaders.BinaryLength..]));
    // }
    //
    // [Theory]
    // [InlineData(0, 0, 0, 0, 0)]
    // [InlineData(ulong.MaxValue, uint.MaxValue, int.MaxValue, int.MaxValue, ulong.MaxValue)]
    // [InlineData(ulong.MaxValue / 2, uint.MaxValue / 2, int.MaxValue / 2, int.MaxValue / 2, ulong.MaxValue / 2)]
    // [InlineData(52, 51, 50, 49, 48)]
    // public void HeadersReadWrite_WithTrash(
    //     ulong magicNumber,
    //     uint version,
    //     int width,
    //     int height,
    //     ulong trash
    // )
    // {
    //     // Arrange
    //     Span<byte> buffer = stackalloc byte[DbHeaders.BinaryLength + 8];
    //     MemoryMarshal.Write(buffer[DbHeaders.BinaryLength..], trash);
    //     var headers = new DbHeaders(magicNumber, version, width, height);
    //
    //     // Act
    //     headers.Write(buffer);
    //     var read = DbHeaders.Read(buffer);
    //
    //     // Assert
    //     Assert.Equal(headers, read);
    //     Assert.Equal(trash, MemoryMarshal.Read<ulong>(buffer[DbHeaders.BinaryLength..]));
    // }
}