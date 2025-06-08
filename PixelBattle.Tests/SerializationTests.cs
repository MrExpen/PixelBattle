using System.Reflection;
using System.Security.Cryptography;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.Tests;

public class SerializationTests
{
    public static TheoryData<Type> BinaryLengthTypes =>
        new(typeof(DbHeaders).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: false, IsInterface: false, IsClass: false})
            .Where(x => x.IsAssignableTo(typeof(IBinarySerializable<>).MakeGenericType(x)))
        );

    [Theory]
    [MemberData(nameof(BinaryLengthTypes))]
    public void GenericReadWriteTest(Type type)
    {
        GetType().GetRuntimeMethods().Single(x => x.Name == nameof(InternalGenericReadWriteTest))
            .MakeGenericMethod(type)
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
}