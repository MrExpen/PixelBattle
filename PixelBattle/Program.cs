// See https://aka.ms/new-console-template for more information

using PixelBattle;

const string path = "test.pbdexpn";

await using (var db = PixelBattleDatabase.Create(path, 1024, 1024))
{
    var t1 = db.SetAsync(0, 0, 1);
    var t2 = db.SetAsync(1, 0, 255);

    await Task.WhenAll(t1, t2);
}

await using var db1 = PixelBattleDatabase.Open(path);

Console.WriteLine();