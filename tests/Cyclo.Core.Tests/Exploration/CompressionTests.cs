using System.Diagnostics;
using System.IO.Compression;
using Xunit.Abstractions;

namespace Cyclo.Core.Tests.Exploration;

// Exploratory measurement, not a benchmark: Stopwatch numbers are rough (proper measurement: BenchmarkDotNet, week 5).
// Compares GZip and Brotli on a large private GPX file: compressed size, ratio, compress and decompress time.
[Trait("Category", "LocalData")]
public class CompressionTests(ITestOutputHelper output)
{
    private const int Runs = 10;

    [Fact]
    public void Compare_GZip_and_Brotli_levels()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "Private", "big-file-perf.gpx");
        byte[] original = File.ReadAllBytes(path);

        output.WriteLine($"Original: {original.Length / 1024.0:F0} KB, median of {Runs} runs after warm-up");
        output.WriteLine($"{"algorithm",-9} | {"level",-12} | {"size [KB]",9} | {"ratio",6} | {"compress [ms]",13} | {"decompress [ms]",15}");
        output.WriteLine(new string('-', 80));

        foreach (string algorithm in new[] { "GZip", "Brotli" })
        foreach (CompressionLevel level in new[] { CompressionLevel.Fastest, CompressionLevel.Optimal, CompressionLevel.SmallestSize })
        {
            byte[] compressed = Compress(algorithm, level, original); // warm-up (JIT)
            Assert.Equal(original, Decompress(algorithm, compressed)); // lossless round-trip, also decompress warm-up

            double compressMs = MedianMs(() => Compress(algorithm, level, original));
            double decompressMs = MedianMs(() => Decompress(algorithm, compressed));
            double ratio = (double)original.Length / compressed.Length;

            output.WriteLine($"{algorithm,-9} | {level,-12} | {compressed.Length / 1024.0,9:F0} | {ratio,5:F1}x | {compressMs,13:F1} | {decompressMs,15:F1}");
        }
    }

    private static byte[] Compress(string algorithm, CompressionLevel level, byte[] data)
    {
        using var destination = new MemoryStream();
        using (Stream compressor = algorithm == "GZip"
                   ? new GZipStream(destination, level)
                   : new BrotliStream(destination, level))
        {
            compressor.Write(data);
        } // compressor must be disposed before reading the destination
        return destination.ToArray();
    }
    
    private static byte[] Decompress(string algorithm, byte[] data)
    {
        using var source = new MemoryStream(data);
        using Stream decompressor = algorithm == "GZip"
            ? new GZipStream(source, CompressionMode.Decompress)
            : new BrotliStream(source, CompressionMode.Decompress);
        using var result = new MemoryStream();
        decompressor.CopyTo(result);
        return result.ToArray();
    }
    
    private static double MedianMs(Func<byte[]> action)
    {
        var times = new double[Runs];
        for (int i = 0; i < Runs; i++)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(times);
        return times[Runs / 2];
    }
}
