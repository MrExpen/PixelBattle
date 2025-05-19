// See https://aka.ms/new-console-template for more information

using PixelBattle;

const string path = "test.pbdexpn";

// await using var pixelBattleDatabase = PixelBattleDatabase.Open(path);
await using var pixelBattleDatabase1 = PixelBattleDatabase.Create(path, 1024, 1024);
