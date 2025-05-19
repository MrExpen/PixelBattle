using System.Reflection;
using System.Runtime.InteropServices;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.Tests;

public class SizeTests
{
    [Theory]
    [InlineData(typeof(Headers), 20)]
    public void SizeOfFields(Type type, int size)
    {
        var fields = type.GetFields().Where(x => !x.IsStatic);
        var membersSize = fields.Select(x => Marshal.SizeOf(x.FieldType)).Sum();

        Assert.Equal(size, membersSize);
    }

    [Theory]
    [InlineData(typeof(Headers))]
    public void SizeOfFieldsBinaryLength(Type type)
    {
        var fields = type.GetFields().Where(x => !x.IsStatic);
        var membersSize = fields.Select(x => Marshal.SizeOf(x.FieldType)).Sum();
        var size = (int)type.GetProperty(nameof(IBinaryLength.BinaryLength))!.GetValue(null)!;

        Assert.Equal(size, membersSize);
    }

}