
using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class ImageRedChannelProcessor : MonoBehaviour
{
    public Texture2D image;

    private const int TestRuns = 20;
    private const int WarmupRuns = 5;
    private const int ChunkSize = 4096;

    [BurstCompile]
    private struct RedSumJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Pixels;
        [WriteOnly] public NativeArray<long> PartialSums;

        public int PixelsPerChunk;

        public void Execute(int index)
        {
            int start = index * PixelsPerChunk;
            int end = start + PixelsPerChunk;

            if (end > Pixels.Length)
                end = Pixels.Length;

            long sum = 0;

            for (int i = start; i < end; i++)
            {
                sum += Pixels[i].r;
            }

            PartialSums[index] = sum;
        }
    }

    void Start()
    {
        if (image == null)
        {
            UnityEngine.Debug.LogError("Assign a texture first!");
            return;
        }

        Color32[] pixels = image.GetPixels32();

        int chunkCount =
            (pixels.Length + ChunkSize - 1) / ChunkSize;

        UnityEngine.Debug.Log(
            $"Image: {image.width} x {image.height}"
        );

        UnityEngine.Debug.Log(
            $"Pixels analysed: {pixels.Length}"
        );

        // Allocate memory BEFORE benchmarking.
        using (var nativePixels = new NativeArray<Color32>(
            pixels, Allocator.Persistent))
        using (var partialSums = new NativeArray<long>(
            chunkCount, Allocator.Persistent))
        {
            var job = new RedSumJob
            {
                Pixels = nativePixels,
                PartialSums = partialSums,
                PixelsPerChunk = ChunkSize
            };

            // Warm up both methods.
            for (int i = 0; i < WarmupRuns; i++)
            {
                CalculateSingle(pixels);
                CalculateParallel(job, partialSums, chunkCount);
            }

            double[] singleTimes = new double[TestRuns];
            double[] parallelTimes = new double[TestRuns];

            long singleTotal = 0;
            long parallelTotal = 0;

            Stopwatch timer = new Stopwatch();

            // Run the single-threaded test 20 times.
            for (int i = 0; i < TestRuns; i++)
            {
                timer.Restart();

                singleTotal = CalculateSingle(pixels);

                timer.Stop();
                singleTimes[i] = timer.Elapsed.TotalMilliseconds;
            }

            // Run the parallel test 20 times.
            for (int i = 0; i < TestRuns; i++)
            {
                timer.Restart();

                parallelTotal = CalculateParallel(
                    job, partialSums, chunkCount
                );

                timer.Stop();
                parallelTimes[i] = timer.Elapsed.TotalMilliseconds;
            }

            UnityEngine.Debug.Log(
                $"Single-thread total: {singleTotal}"
            );

            UnityEngine.Debug.Log(
                $"Parallel total: {parallelTotal}"
            );

            UnityEngine.Debug.Log(
                $"Results match: {singleTotal == parallelTotal}"
            );

            UnityEngine.Debug.Log(
                $"Single-thread average: {Average(singleTimes):F3} ms"
            );

            UnityEngine.Debug.Log(
                $"Parallel average: {Average(parallelTimes):F3} ms"
            );

            UnityEngine.Debug.Log(
                $"Single-thread median: {Median(singleTimes):F3} ms"
            );

            UnityEngine.Debug.Log(
                $"Parallel median: {Median(parallelTimes):F3} ms"
            );
        }
    }

    // Normal calculation on the main thread.
    private static long CalculateSingle(Color32[] pixels)
    {
        long total = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            total += pixels[i].r;
        }

        return total;
    }

    // Parallel calculation using Unity Jobs.
    private static long CalculateParallel(
        RedSumJob job,
        NativeArray<long> partialSums,
        int chunkCount)
    {
        JobHandle handle = job.Schedule(chunkCount, 1);
        handle.Complete();

        long total = 0;

        for (int i = 0; i < partialSums.Length; i++)
        {
            total += partialSums[i];
        }

        return total;
    }

    // Calculate the mean.
    private static double Average(double[] values)
    {
        double total = 0;

        foreach (double value in values)
        {
            total += value;
        }

        return total / values.Length;
    }

    // Calculate the middle measurement.
    private static double Median(double[] values)
    {
        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);

        int middle = sorted.Length / 2;

        if (sorted.Length % 2 == 0)
            return (sorted[middle - 1] + sorted[middle]) / 2;

        return sorted[middle];
    }
}
