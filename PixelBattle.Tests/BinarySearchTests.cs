using System.Reflection;

namespace PixelBattle.Tests;

public class BinarySearchTests
{
    [Theory]
    [InlineData(nameof(BinarySearch1), "1, 2, 3, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8", 5, 4)]
    [InlineData(nameof(BinarySearch1), "1, 2, 3, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8", 9, 14)]
    [InlineData(nameof(BinarySearch1), "1, 2, 3, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8", 0, 0)]
    [InlineData(nameof(BinarySearch1), "1, 2, 3, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8", 1, 1)]
    [InlineData(nameof(BinarySearch1), "1, 2, 3, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8", 4, 4)]
    public void AlgorithmChoice(string funcName, string arrayString, long search, int expectedIndex)
    {
        //Arrange
        var funcInfo = GetType().GetMethod(funcName, BindingFlags.Static | BindingFlags.NonPublic);
        var func = (Func<long[], long, long>)Delegate.CreateDelegate(typeof(Func<long[], long, long>), funcInfo);
        long[] array = arrayString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse)
            .ToArray();

        //Act
        var index = func(array, search);

        //Assert
        Assert.Equal(expectedIndex, index);
    }


    private static long BinarySearch1(long[] array, long search)
    {
        long l = 0;
        long r = array.Length;

        while (l < r)
        {
            var m = l + (r - l) / 2;
            var mV = array[m];
            if (mV < search)
            {
                l = m + 1;
            }
            else
            {
                r = m;
            }
        }

        if (l + 1 >= array.Length || array[l] != search || array[l + 1] == search)
        {
            return l;
        }

        return l + 1;
    }
}