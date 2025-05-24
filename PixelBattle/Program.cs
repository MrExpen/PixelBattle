// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using PixelBattle;

const string path = "test.pbdexpn";

await using (var db = PixelBattleDatabase.Create(path, 1024, 1024, 4096)) ;

List<Task> tasks = [];
Stopwatch sw;
await using (var db1 = await PixelBattleDatabase.OpenAsync(path))
{
    sw = Stopwatch.StartNew();
    for (long i = 0; i < 1_000_000; i++)
    {
        var task = db1.SetAsync(Random.Shared.Next(0, db1.Width), Random.Shared.Next(0, db1.Height),
            (byte)Random.Shared.Next());
        tasks.Add(task);
    }

    await Task.WhenAll(tasks);
}

Console.WriteLine(sw.Elapsed);

Console.WriteLine();