// See https://aka.ms/new-console-template for more information

using PixelBattle;

const string path = "test.pbdexpn";


await using var db = PixelBattleDatabase.Create(path, 1024, 1024);

await db.SetAsync(0, 0, 1);
await db.SetAsync(1, 0, 255);

Console.WriteLine();