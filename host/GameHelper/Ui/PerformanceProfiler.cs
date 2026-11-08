// <copyright file="PerformanceProfiler.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Ui;

using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
///     Performance profiler for optimization purposes.
/// </summary>
public static class PerformanceProfiler
{
    internal static readonly double NsPerTick = 1000000000.0 / Stopwatch.Frequency;
    private static readonly ConcurrentDictionary<string, ProfileData> ProfileData = new();
    private static readonly ConcurrentDictionary<string, double> CurrentFrameNs = new();
    private static readonly ConcurrentDictionary<string, int> CurrentFrameCounts = new();
    

    public static IDisposable? Profile(string namespaceName, string methodName)
    {
        if (!Core.GHSettings.ShowPerfProfiler)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        return new ProfileDisposable($"{namespaceName}.{methodName}", stopwatch, ProfileData, CurrentFrameNs, CurrentFrameCounts);
    }

    public static void StartFrame()
    {
        if (!Core.GHSettings.ShowPerfProfiler)
        {
            return;
        }
            
        CurrentFrameNs.Clear();
        CurrentFrameCounts.Clear();
    }
        
    public static void EndFrame()
    {
        if (!Core.GHSettings.ShowPerfProfiler)
        {
            return;
        }

        // Add frame samples for each profiled method
        foreach (var kvp in CurrentFrameNs)
        {
            var key = kvp.Key;
            var frameNs = kvp.Value;
            ProfileData.AddOrUpdate(key,
                _ => {
                    var pd = new ProfileData();
                    pd.AddFrameSample(frameNs);
                    return pd;
                },
                (k, existing) => {
                    existing.AddFrameSample(frameNs);
                    return existing;
                });
        }
    }
}

internal class ProfileData
{
    private const int WindowSize = 100;
    private int totalCount;
    private long sumTicks;
    private double sumFrameNs;
    private readonly ConcurrentQueue<long> recentTicks = new();
    private readonly ConcurrentQueue<double> recentFrameNs = new();
    public int Count => totalCount;

    public double AverageTicks => !recentTicks.IsEmpty ? (double)sumTicks / recentTicks.Count : 0.0;
    public double AverageFrameNs => !recentFrameNs.IsEmpty ? sumFrameNs / recentFrameNs.Count : 0.0;

    public void AddSample(long ticks)
    {
        Interlocked.Increment(ref totalCount);
        recentTicks.Enqueue(ticks);
        Interlocked.Add(ref sumTicks, ticks);
        while (recentTicks.Count > WindowSize)
        {
            if (recentTicks.TryDequeue(out var old))
            {
                Interlocked.Add(ref sumTicks, -old);
            }
        }
    }

    public void AddFrameSample(double ns)
    {
        recentFrameNs.Enqueue(ns);
        // F-182: atomic add - was raw `+=`, racy on parallel ProfileDisposable.Dispose
        // calls that update CurrentFrameNs from worker threads. Interlocked has no
        // double Add overload, so use the standard CompareExchange loop pattern.
        InterlockedAddDouble(ref sumFrameNs, ns);
        while (recentFrameNs.Count > WindowSize)
        {
            if (recentFrameNs.TryDequeue(out double old))
            {
                InterlockedAddDouble(ref sumFrameNs, -old);
            }
        }
    }

    private static double InterlockedAddDouble(ref double location, double value)
    {
        double current, computed;
        do
        {
            current = location;
            computed = current + value;
        }
        while (Interlocked.CompareExchange(ref location, computed, current) != current);
        return computed;
    }
}

internal class ProfileDisposable(
    string methodName,
    Stopwatch stopwatch,
    ConcurrentDictionary<string, ProfileData> profileData,
    ConcurrentDictionary<string, double> currentFrameNs,
    ConcurrentDictionary<string, int> currentFrameCounts)
    : IDisposable
{
    public void Dispose()
    {
        stopwatch.Stop();
        var elapsedTicks = stopwatch.ElapsedTicks;
        var elapsedNs = elapsedTicks * PerformanceProfiler.NsPerTick;
        profileData.AddOrUpdate(methodName,
            _ => { var pd = new ProfileData(); pd.AddSample(elapsedTicks); return pd; },
            (key, existing) => { existing.AddSample(elapsedTicks); return existing; });
        currentFrameNs.AddOrUpdate(methodName,
            _ => elapsedNs,
            (key, existing) => existing + elapsedNs);
        currentFrameCounts.AddOrUpdate(methodName,
            _ => 1,
            (key, existing) => existing + 1);
    }
}