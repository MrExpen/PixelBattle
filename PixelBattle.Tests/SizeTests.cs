using System.Runtime.InteropServices;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.Tests;

public class SizeTests
{
    public static TheoryData<Type> BinaryLengthTypes =>
        new(typeof(DbHeaders).Assembly.GetTypes()
            .Where(x => x.IsAssignableTo(typeof(IBinaryLength)))
            .Where(x => x is { IsAbstract: false, IsInterface: false })
        );

    [Theory]
    [MemberData(nameof(BinaryLengthTypes))]
    public void SizeOfFieldsBinaryLength(Type type)
    {
        var fields = type.GetFields().Where(x => !x.IsStatic);
        var membersSize = fields.Select(x => GetTypeSize(x.FieldType)).Sum();
        var size = (int)type.GetProperty(nameof(IBinaryLength.BinaryLength))!.GetValue(null)!;

        Assert.Equal(membersSize, size);
    }

    private static int GetTypeSize(Type t)
    {
        if (t.IsPrimitive || t.IsEnum)
        {
            return t.IsEnum ? Marshal.SizeOf(Enum.GetUnderlyingType(t)) : Marshal.SizeOf(t);
        }

        throw new NotImplementedException();
    }
}