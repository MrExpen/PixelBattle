// See https://aka.ms/new-console-template for more information

using PixelBattle;

const string path = "test.pbdexpn";

await using var driver = PixelBattleDatabaseDriver.Create(path, 1024, 1024, 4096);

await driver.SetAsync(0, 0, 1);

Console.WriteLine();

// await using (var db = PixelBattleDatabase.Create(path, 1024, 1024, 4096)) ;
//
// List<Task> tasks = [];
// await using var db1 = await PixelBattleDatabase.OpenAsync(path);
//
// var enumerable = db1.GetUpdateEnumerable();
//
// var taskPrint = Task.Run(async void () =>
// {
//     try
//     {
//         await foreach (var update in enumerable)
//         {
//             Console.WriteLine(update);
//         }
//
//         Console.WriteLine("done");
//     }
//     catch (Exception e)
//     {
//         Console.WriteLine(e);
//     }
// });
//
// var sw = Stopwatch.StartNew();
// for (long i = 0; i < 1_000_000; i++)
// {
//     var task = db1.SetAsync(Random.Shared.Next(0, db1.Width), Random.Shared.Next(0, db1.Height),
//         (byte)Random.Shared.Next());
//     tasks.Add(task);
// }
//
// await Task.WhenAll(tasks);
// Console.WriteLine(sw.Elapsed);
//
//
// await Task.Delay(TimeSpan.FromMinutes(5));
//
// Console.WriteLine();