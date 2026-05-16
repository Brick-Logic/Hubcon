namespace HubconTestClient;

using System;
using System.Diagnostics;
using System.Threading;

public class LatencyHistogram
{
    // Usamos baldes de 10 microsegundos para alta precisión.
    // 1000 baldes cubren de 0ms a 10ms. El último balde es para > 10ms.
    private const int BucketSizeMicros = 10;
    private const int BucketCount = 1001;
    private readonly long[] _buckets = new long[BucketCount];
    public long totalSamples = 0;

    public void Record(long startTicks)
    {
        long end = Stopwatch.GetTimestamp();
        double elapsedMicros = (double)(end - startTicks) * 1_000_000 / Stopwatch.Frequency;
        
        int bucketIndex = (int)(elapsedMicros / BucketSizeMicros);
        if (bucketIndex >= BucketCount) bucketIndex = BucketCount - 1;

        Interlocked.Increment(ref _buckets[bucketIndex]);
        Interlocked.Increment(ref totalSamples);
    }

    public void PrintReport()
    {
        long total = Volatile.Read(ref totalSamples);
        if (total == 0) return;

        Console.WriteLine("\n--- Reporte de Latencia ---");
        Console.WriteLine($"Muestras totales: {total}");
        Console.WriteLine($"P50: {GetPercentile(50):F2} ms");
        Console.WriteLine($"P95: {GetPercentile(95):F2} ms");
        Console.WriteLine($"P99: {GetPercentile(99):F2} ms");
        Console.WriteLine($"Máx (>): {GetPercentile(100):F2} ms");
    }

    private double GetPercentile(double percentile)
    {
        long target = (long)(percentile / 100.0 * totalSamples);
        long currentSum = 0;

        for (int i = 0; i < BucketCount; i++)
        {
            currentSum += _buckets[i];
            if (currentSum >= target)
            {
                return (i * BucketSizeMicros) / 1000.0; // Convertir a ms
            }
        }
        return (BucketCount * BucketSizeMicros) / 1000.0;
    }
}