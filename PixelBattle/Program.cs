// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using PixelBattle;

const string path = "test.pbdexpn";

await using (var db = PixelBattleDatabase.Create(path, 1024, 1024, 4096)) ;

{
    List<Task> tasks = [];
    await using var db1 = await PixelBattleDatabase.OpenAsync(path);

    var sw = Stopwatch.StartNew();
    for (long i = 0; i < 10_000_000; i++)
    {
        var task = db1.SetAsync(Random.Shared.Next(0, db1.Width), Random.Shared.Next(0, db1.Height),
            (byte)Random.Shared.Next());
        tasks.Add(task);
    }

    await Task.WhenAll(tasks);

    Console.WriteLine(sw.Elapsed);

    long sum1 = 0;
    for (int i = 0; i < db1.ChunksCount; i++)
    {
        sum1 += db1.GetChunkVersion(i);
    }

    Console.WriteLine(sum1);
}

await using var db2 = await PixelBattleDatabase.OpenAsync(path);

long sum = 0;
for (int i = 0; i < db2.ChunksCount; i++)
{
    var version = db2.GetChunkVersion(i);
    sum += version;
    Console.WriteLine($"{i,-3}: {version,-5}");
}

Console.WriteLine(sum);

Console.WriteLine();