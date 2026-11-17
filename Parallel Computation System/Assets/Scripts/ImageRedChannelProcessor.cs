
using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class ImageRedChannelProcessor : MonoBehaviour
{
    public Texture2D image;

    [BurstCompile]
    private struct RedSumJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Pixels;
        [WriteOnly] public NativeArray<long> PartialSums;

        public int ChunkSize;

        public void Execute(int index)
        {
            int start = index * ChunkSize;
            int end = Math.Min(start + ChunkSize, Pixels.Length);

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
            UnityEngine.Debug.LogError("Please assign a texture.");
            return;
        }

        Color32[] pixels = image.GetPixels32();

        // Ordinary single-threaded calculation
        Stopwatch timer = Stopwatch.StartNew();

        long singleTotal = 0;

        foreach (Color32 pixel in pixels)
        {
            singleTotal += pixel.r;
        }

        timer.Stop();

        UnityEngine.Debug.Log(
            $"Single-thread total: {singleTotal} | " +
            $"Time: {timer.Elapsed.TotalMilliseconds:F3} ms"
        );

        // Parallel computation using Unity Jobs
        const int chunkSize = 4096;
        int chunkCount =
            (pixels.Length + chunkSize - 1) / chunkSize;

        using (var nativePixels =
            new NativeArray<Color32>(
                pixels, Allocator.TempJob))
        using (var partialSums =
            new NativeArray<long>(
                chunkCount, Allocator.TempJob))
        {
            var job = new RedSumJob
            {
                Pixels = nativePixels,
                PartialSums = partialSums,
                ChunkSize = chunkSize
            };

            timer.Restart();

            JobHandle handle = job.Schedule(chunkCount, 1);
            handle.Complete();

            long parallelTotal = 0;

            for (int i = 0; i < partialSums.Length; i++)
            {
                parallelTotal += partialSums[i];
            }

            timer.Stop();

            UnityEngine.Debug.Log(
                $"Parallel total: {parallelTotal} | " +
                $"Time: {timer.Elapsed.TotalMilliseconds:F3} ms"
            );

            UnityEngine.Debug.Log(
                $"Results match: {singleTotal == parallelTotal}"
            );
        }
    }
}
