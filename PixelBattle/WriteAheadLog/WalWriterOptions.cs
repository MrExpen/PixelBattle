namespace PixelBattle.WriteAheadLog;

public class WalWriterOptions
{
    public int MaxQueueSize { get; init; } = 4096;

    public int MaxBatchCommitSize { get; init; } = 1024;
}