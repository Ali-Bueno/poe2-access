// <copyright file="MemoryReadDiagnostics.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Ui;

using System;
using System.Collections.Concurrent;
using System.Threading;

/// <summary>
///     Aggregates failed process-memory reads per call site so torn reads (the game mutating
///     data while we read it) can be told apart from a real offset/struct-layout breakage.
///     Recording is gated on <see cref="Settings.State.ShowMemoryDiagnostics"/> and costs
///     nothing (no stack walk, no bookkeeping) when the setting is off.
/// </summary>
public static class MemoryReadDiagnostics
{
    private const int MaxTrackedAddressesPerKey = 1024;
    private static readonly ConcurrentDictionary<string, FailureStat> Stats = new();

    /// <summary>
    ///     Records a failed memory read. Callers must check
    ///     <see cref="Settings.State.ShowMemoryDiagnostics"/> before computing the (relatively
    ///     expensive) caller string, so this method assumes recording is wanted.
    /// </summary>
    /// <param name="typeName">name of the type that failed to read.</param>
    /// <param name="caller">"Assembly!Type.Method" of the originating call site.</param>
    /// <param name="address">the address that failed to read.</param>
    public static void RecordFailure(string typeName, string caller, long address)
    {
        var key = $"{caller}  ({typeName})";
        var stat = Stats.GetOrAdd(key, _ => new FailureStat());
        Interlocked.Increment(ref stat.Total);
        stat.LastTicks = Environment.TickCount64;

        // Bound memory: once we've seen enough distinct addresses, only keep counting the
        // ones we already track. The capped set is still plenty to expose recurrence.
        if (stat.Addresses.Count < MaxTrackedAddressesPerKey || stat.Addresses.ContainsKey(address))
        {
            stat.Addresses.AddOrUpdate(address, 1, (_, c) => c + 1);
        }
        else
        {
            Interlocked.Increment(ref stat.UntrackedAddressHits);
        }
    }

    private sealed class FailureStat
    {
        public long Total;
        public long UntrackedAddressHits;
        public long LastTicks;
        public readonly ConcurrentDictionary<long, int> Addresses = new();
    }
}
