namespace HubconTestClient;

using System;
using System.Diagnostics;
using System.Threading;


public class LatencyHistogram
{
    private const int BucketSizeMicros = 10;
    private const int BucketCount = 10_001; 
    private readonly long[] _buckets = new long[BucketCount];
    
    private long _totalSamples;
    private long _maxTicks;

    public long TotalSamples => Volatile.Read(ref _totalSamples);

    public void Record(long startTicks)
    {
        long end = Stopwatch.GetTimestamp();
        long elapsedTicks = end - startTicks;
        if (elapsedTicks < 0) elapsedTicks = 0;

        UpdateMax(elapsedTicks);

        double elapsedMicros = (double)elapsedTicks * 1_000_000 / Stopwatch.Frequency;
        
        int bucketIndex = (int)(elapsedMicros / BucketSizeMicros);
        if (bucketIndex >= BucketCount) bucketIndex = BucketCount - 1;

        Interlocked.Increment(ref _buckets[bucketIndex]);
        Interlocked.Increment(ref _totalSamples);
    }

    public void PrintReport()
    {
        long total = TotalSamples;
        if (total == 0)
        {
            Console.WriteLine("\n--- Reporte de Latencia: Sin muestras ---");
            return;
        }

        long maxTicks = Volatile.Read(ref _maxTicks);
        double maxMicros = (double)maxTicks * 1_000_000 / Stopwatch.Frequency;

        Console.WriteLine("\n--- Reporte de Latencia ---");
        Console.WriteLine($"Muestras totales: {total:N0}");
        Console.WriteLine($"P50 : {FormatMicros(GetPercentileMicros(50, total))}");
        Console.WriteLine($"P95 : {FormatMicros(GetPercentileMicros(95, total))}");
        Console.WriteLine($"P99 : {FormatMicros(GetPercentileMicros(99, total))}");
        Console.WriteLine($"Max : {FormatMicros(maxMicros)}");
    }

    private double GetPercentileMicros(double percentile, long total)
    {
        long target = (long)Math.Ceiling(percentile / 100.0 * total);
        if (target == 0) target = 1;

        long currentSum = 0;

        for (int i = 0; i < BucketCount; i++)
        {
            currentSum += Volatile.Read(ref _buckets[i]);
            if (currentSum >= target)
            {
                return (i + 1) * BucketSizeMicros; 
            }
        }

        long maxTicks = Volatile.Read(ref _maxTicks);
        return (double)maxTicks * 1_000_000 / Stopwatch.Frequency;
    }

    private void UpdateMax(long elapsedTicks)
    {
        long currentMax = Volatile.Read(ref _maxTicks);
        while (elapsedTicks > currentMax)
        {
            long oldMax = Interlocked.CompareExchange(ref _maxTicks, elapsedTicks, currentMax);
            if (oldMax == currentMax) break;
            currentMax = oldMax;
        }
    }

    private static string FormatMicros(double micros)
    {
        if (micros >= 1_000_000)
            return $"{micros / 1_000_000.0:F2} s"; 
        if (micros >= 1_000)
            return $"{micros / 1_000.0:F2} ms";
        
        return $"{micros:F0} µs";
    }
}