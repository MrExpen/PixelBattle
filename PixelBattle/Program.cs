// See https://aka.ms/new-console-template for more information

using PixelBattle;

const string path = "test.pbdexpn";


await using (var pixelBattleDatabase = PixelBattleDatabase.Create(path, 1024, 1024)) ;
await using var db = PixelBattleDatabase.Open(path);

db.Set(0, 0, 1);
db.Set(1, 0, 255);
var data = db.GetAll();

Console.WriteLine();