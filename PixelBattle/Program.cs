// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using PixelBattle;

const string path = "test.pbdexpn";

// await using (var db = PixelBattleDatabase.Create(path, 1024, 1024)) ;
// {
//     var t1 = db.SetAsync(0, 0, 1);
//     var t2 = db.SetAsync(1, 0, 255);
//
//     await Task.WhenAll(t1, t2);
// }

await using var db1 = await PixelBattleDatabase.OpenAsync(path);

var sw = Stopwatch.StartNew();
List<Task> tasks = [];
for (int i = 0; i < 10_000_000; i++)
{
    var task = db1.SetAsync(Random.Shared.Next(0, db1.Width), Random.Shared.Next(0, db1.Height),
        (byte)Random.Shared.Next());
    tasks.Add(task);
}

await Task.WhenAll(tasks);

Console.WriteLine(sw.Elapsed);

Console.WriteLine();