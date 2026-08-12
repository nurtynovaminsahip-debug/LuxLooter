namespace LootCollector.Core;

public sealed class BatchConfig
{
    public int Size = 1;

    public double LaunchDelaySec = 10.0;      // задержка между запусками альтов

    public double ActiveTimeoutSec = 900.0;   // 15 минут на активность (было 600)

    public double JoinDetectSec = 80.0;       // общее время на заход в игру (было 35-50)

    public double AttachTimeoutSec = 60.0;    // время ожидания .run (было 20-35)

    public int MaxRetries = 2;
}