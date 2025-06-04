// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using PixelBattle.Benchmarks;

BenchmarkRunner.Run<NotifyBenchmark>();