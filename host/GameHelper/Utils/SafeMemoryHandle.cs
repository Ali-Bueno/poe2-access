// <copyright file="SafeMemoryHandle.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using GameOffsets.Natives;
    using Microsoft.Win32.SafeHandles;
    using ProcessMemoryUtilities.Managed;
    using ProcessMemoryUtilities.Native;

    /// <summary>
    ///     Handle to a process.
    /// </summary>
    internal class SafeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        ///     Max valid user-mode address on 64-bit Windows (48-bit addressing).
        /// </summary>
        private const long MaxUserModeAddress = 0x7FFFFFFFFFFF;

        /// <summary>
        ///     Lowest address that can ever back a real allocation. The first 64 KiB is the
        ///     reserved null-pointer partition on Windows x64, and allocation granularity is
        ///     64 KiB, so anything below this is never a valid pointer. Game pointers live far
        ///     above this (module base 0x140000000+, heap in the TiB range), so this floor never
        ///     rejects a legitimate read — it only catches data values (floats, small ints,
        ///     string fragments) that a torn read fed in where a pointer was expected.
        /// </summary>
        private const long MinValidAddress = 0x10000;

        /// <summary>
        ///     A std::map smaller than this is walked on the calling thread: a town holds some 45 entities, and waking the
        ///     pool's workers for them cost more than their reads (profile, 2026-11-08). Fights, with hundreds, stay parallel.
        /// </summary>
        private const int ParallelMinNodes = 128;

        /// <summary>Gets the reads of the game's memory made so far (each a system call), by the type read: the host logs them per frame.</summary>
        internal static IEnumerable<KeyValuePair<string, long>> ReadsByType =>
            counters.Select(pair => new KeyValuePair<string, long>(pair.Key, Interlocked.Read(ref pair.Value.Value)));

        private static readonly ConcurrentDictionary<string, ReadCounter> counters = new();

        internal sealed class ReadCounter
        {
            public long Value;
        }

        /// <summary>One counter per type read, found without a lookup on the hot path.</summary>
        private static class ReadCount<T>
        {
            public static readonly ReadCounter Of = counters.GetOrAdd(typeof(T).Name, _ => new ReadCounter());
        }

        /// <summary>
        ///     Required by SafeHandle infrastructure for finalizer / marshaling support.
        ///     Private to prevent callers accidentally constructing a zombie handle
        ///     without a PID — see audit F-034. Real construction must go through
        ///     the <see cref="SafeMemoryHandle(int)"/> ctor.
        /// </summary>
        private SafeMemoryHandle()
            : base(true)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SafeMemoryHandle" /> class.
        /// </summary>
        /// <param name="processId">processId you want to access.</param>
        internal SafeMemoryHandle(int processId)
            : base(true)
        {
            var handle = NativeWrapper.OpenProcess(ProcessAccessFlags.VirtualMemoryRead, processId);
            if (NativeWrapper.HasError)
            {
                Console.WriteLine($"Failed to open a new handle 0x{handle:X}" +
                                  $" due to ErrorNo: {NativeWrapper.LastError}");
            }
            else
            {
                Console.WriteLine($"Opened a new handle using IntPtr 0x{handle:X}");
            }

            this.SetHandle(handle);
        }

        /// <summary>
        ///     Reads the process memory as type T.
        /// </summary>
        /// <typeparam name="T">type of data structure to read.</typeparam>
        /// <param name="address">address to read the data from.</param>
        /// <returns>data from the process in T format.</returns>
        internal T ReadMemory<T>(IntPtr address)
            where T : unmanaged
        {
            if (this.TryReadMemory<T>(address, out var result))
            {
                return result;
            }

            // Only a genuine read failure on a plausible address warrants a console error;
            // an out-of-range address is silently skipped (historical behaviour). Both cases
            // are still captured by the diagnostics window via TryReadMemory.
            if (!this.IsInvalid && IsValidAddress(address))
            {
                Console.WriteLine("ERROR: Failed To Read the Memory (T)" +
                                  $" due to Error Number: 0x{NativeWrapper.LastError:X} on " +
                                  $"adress 0x{address.ToInt64():X} for type {typeof(T).Name}");
            }

            return default;
        }

        /// <summary>
        ///     Reads the process memory as type T without logging on failure. Returns false
        ///     (and <paramref name="result"/> = default) when the handle is invalid, the
        ///     address fails the <see cref="IsValidAddress"/> sanity check, or the underlying
        ///     read fails. Use this on hot, inherently-racy paths (e.g. walking a live,
        ///     concurrently-mutated container) where torn reads are expected and recoverable,
        ///     so they don't flood the log. Use <see cref="ReadMemory{T}"/> elsewhere so a
        ///     genuine offset/layout breakage still surfaces.
        /// </summary>
        /// <typeparam name="T">type of data structure to read.</typeparam>
        /// <param name="address">address to read the data from.</param>
        /// <param name="result">data read from the process, or default on failure.</param>
        /// <returns>true if the read succeeded; otherwise false.</returns>
        internal bool TryReadMemory<T>(IntPtr address, out T result)
            where T : unmanaged
        {
            result = default;
            if (this.IsInvalid || !IsValidAddress(address))
            {
                return false;
            }

            if (StructRemap<T>.Segments is { } moved)
            {
                return this.TryReadMoved(address, moved, out result);
            }

            try
            {
                Interlocked.Increment(ref ReadCount<T>.Of.Value);
                if (!NativeWrapper.ReadProcessMemory(this.handle, address, ref result))
                {
                    result = default;
                    return false;
                }

                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>Reads a struct whose fields a game patch moved (<see cref="StructRemap{T}"/>).</summary>
        private bool TryReadMoved<T>(IntPtr address, RemapSegment[] moved, out T result)
            where T : unmanaged
        {
            result = default;
            var target = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            var raw = this.ReadMemoryArray<byte>(address, StructRemapper.ReadSize(target.Length, moved));
            if (raw.Length == 0)
            {
                return false;
            }

            StructRemapper.Apply(raw, target, moved);
            return true;
        }

        /// <summary>
        ///     Cheap sanity check that an address could plausibly back a real allocation in
        ///     the target process. Rejects the reserved low memory range and addresses beyond
        ///     the 48-bit user-mode limit. Does not guarantee the address is currently mapped.
        /// </summary>
        /// <param name="address">address to check.</param>
        /// <returns>true if the address is within the plausible user-mode range.</returns>
        internal static bool IsValidAddress(IntPtr address)
        {
            var addr = address.ToInt64();
            return addr >= MinValidAddress && addr <= MaxUserModeAddress;
        }

        /// <summary>
        ///     Reads the std::vector into an array.
        /// </summary>
        /// <typeparam name="T">Object type to read.</typeparam>
        /// <param name="nativeContainer">StdVector address to read from.</param>
        /// <returns>An array of elements of type T.</returns>
        internal T[] ReadStdVector<T>(StdVector nativeContainer)
            where T : unmanaged
        {
            var typeSize = Marshal.SizeOf<T>();
            var length = nativeContainer.Last.ToInt64() - nativeContainer.First.ToInt64();
            if (length <= 0 || length % typeSize != 0 || length > 50_000_000)
            {
                return Array.Empty<T>();
            }

            return this.ReadMemoryArray<T>(nativeContainer.First, (int)length / typeSize);
        }

        /// <summary>
        ///     Reads the process memory as an array.
        /// </summary>
        /// <typeparam name="T">Array type to read.</typeparam>
        /// <param name="address">memory address to read from.</param>
        /// <param name="nsize">total array elements to read.</param>
        /// <returns>
        ///     An array of type T and of size nsize. In case or any error it returns empty array.
        /// </returns>
        internal T[] ReadMemoryArray<T>(IntPtr address, int nsize)
            where T : unmanaged
        {
            if (this.IsInvalid || !IsValidAddress(address) || nsize <= 0)
            {
                return Array.Empty<T>();
            }

            var buffer = new T[nsize];
            try
            {
                // Array/blob/string reads are inherently speculative — the source pointer comes
                // from a container (std::vector/string) that may be torn or stale, so failures
                // and short reads are routine and recoverable. Record them for the diagnostics
                // window but keep the console clean, matching TryReadMemory (audit: torn-read noise).
                var expectedBytes = (long)nsize * Marshal.SizeOf<T>();
                Interlocked.Increment(ref ReadCount<T[]>.Of.Value);
                if (!NativeWrapper.ReadProcessMemoryArray(this.handle, address, buffer, out var numBytesRead) ||
                    numBytesRead.ToInt64() < expectedBytes)
                {
                    return Array.Empty<T>();
                }

                return buffer;
            }
            catch
            {
                return Array.Empty<T>();
            }
        }

        /// <summary>
        ///     Reads the std::string. String read is in ASCII format.
        /// </summary>
        /// <param name="nativecontainer">native object of std::string.</param>
        /// <returns>string.</returns>
        internal string ReadStdString(StdString nativecontainer)
        {
            const int MaxAllowed = 1000;
            if (nativecontainer.Length <= 0 ||
                nativecontainer.Length > MaxAllowed ||
                nativecontainer.Capacity <= 0 ||
                nativecontainer.Capacity > MaxAllowed)
            {
                return string.Empty;
            }

            if (nativecontainer.Capacity <= 15)
            {
                var buffer = BitConverter.GetBytes(nativecontainer.Buffer.ToInt64());
                var ret = Encoding.ASCII.GetString(buffer);
                buffer = BitConverter.GetBytes(nativecontainer.ReservedBytes.ToInt64());
                ret += Encoding.ASCII.GetString(buffer);
                if (nativecontainer.Length < ret.Length)
                {
                    return ret[..nativecontainer.Length];
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                var buffer = this.ReadMemoryArray<byte>(nativecontainer.Buffer, nativecontainer.Length);
                return Encoding.ASCII.GetString(buffer);
            }
        }

        /// <summary>
        ///     Reads the std::wstring. String read is in unicode format.
        /// </summary>
        /// <param name="nativecontainer">native object of std::wstring.</param>
        /// <returns>string.</returns>
        internal string ReadStdWString(StdWString nativecontainer)
        {
            const int MaxAllowed = 1000;
            if (nativecontainer.Length <= 0 ||
                nativecontainer.Length > MaxAllowed ||
                nativecontainer.Capacity <= 0 ||
                nativecontainer.Capacity > MaxAllowed)
            {
                return string.Empty;
            }

            if (nativecontainer.Capacity <= 8)
            {
                var buffer = BitConverter.GetBytes(nativecontainer.Buffer.ToInt64());
                var ret = Encoding.Unicode.GetString(buffer);
                buffer = BitConverter.GetBytes(nativecontainer.ReservedBytes.ToInt64());
                ret += Encoding.Unicode.GetString(buffer);
                if (nativecontainer.Length < ret.Length)
                {
                    return ret[..nativecontainer.Length];
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                var buffer = this.ReadMemoryArray<byte>(nativecontainer.Buffer, nativecontainer.Length * 2);
                return Encoding.Unicode.GetString(buffer);
            }
        }

        /// <summary>
        ///     Reads the string.
        /// </summary>
        /// <param name="address">pointer to the string.</param>
        /// <returns>string read.</returns>
        internal string ReadString(IntPtr address)
        {
            var buffer = this.ReadMemoryArray<byte>(address, 128);
            var count = Array.IndexOf<byte>(buffer, 0x00, 0);
            if (count > 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, count);
            }

            return string.Empty;
        }

        /// <summary>
        ///     Reads Unicode string when string length isn't know.
        ///     Use  <see cref="ReadStdWString" /> if string length is known.
        /// </summary>
        /// <param name="address">points to the Unicode string pointer.</param>
        /// <returns>string read from the memory.</returns>
        internal string ReadUnicodeString(IntPtr address)
        {
            var buffer = this.ReadMemoryArray<byte>(address, 256);
            var count = 0x00;
            for (var i = 0; i < buffer.Length - 2; i++)
            {
                if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00)
                {
                    count = i % 2 == 0 ? i : i + 1;
                    break;
                }
            }

            // let's not return a string if null isn't found.
            if (count == 0)
            {
                return string.Empty;
            }

            var ret = Encoding.Unicode.GetString(buffer, 0, count);
            return ret;
        }

        /// <summary>
        ///     Reads the StdMap in parallel and execute onValue function on each node that isn't null
        /// </summary>
        /// <typeparam name="TKey">stdmap key type</typeparam>
        /// <typeparam name="TValue">stdmap value type</typeparam>
        /// <param name="nativeContainer">native object pointing to the std::map</param>
        /// <param name="maxSizeAllowed">to remove infinite loops, function will return upon reaching this number</param>
        /// <param name="enableCounting">extract more juice from the cpu</param>
        /// <param name="onEachNotNullNode">function to execute on each std map node that isn't null.</param>
        /// <returns>total nodes/childrens in the stdmap</returns>
        internal int ReadStdMap<TKey, TValue>(StdMap nativeContainer, int maxSizeAllowed, bool enableCounting, Func<TKey, TValue, bool> onEachNotNullNode)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            if (nativeContainer.Size <= 0 || nativeContainer.Size > maxSizeAllowed)
            {
                return 0;
            }

            var head = this.ReadMemory<StdMapNode<TKey, TValue>>(nativeContainer.Head);
            var parent = this.ReadMemory<StdMapNode<TKey, TValue>>(head.Parent);
            var first64Childrens = new Queue<StdMapNode<TKey, TValue>>(64);
            if (nativeContainer.Size < ParallelMinNodes)
            {
                return processSubTree(first64Childrens, parent, maxSizeAllowed);
            }

            // processing first 63 nodes will gives us 64 childrens
            // in the first64Childrens list (assuming there is no node with just 1 child).
            // TODO: Benchmark 64 childrens
            var totalChildrenProcessed = processSubTree(first64Childrens, parent, 64);

            // then Parallel.ForEach loop will process those 32 childrens in parallel
            Parallel.ForEach(first64Childrens,
                new ParallelOptions() { MaxDegreeOfParallelism = Core.GHSettings.EntityReaderMaxDegreeOfParallelism },
                // executed once per task/thread
                () => { return (new Queue<StdMapNode<TKey, TValue>>(2000), new int()); },
                // executed once per iteration
                (first32Child, _, _, localState) =>
                {
                    localState.Item2 += processSubTree(localState.Item1, first32Child, maxSizeAllowed / first64Childrens.Count);
                    return localState;
                },
                // executed once per task/thread
                localFinal =>
                {
                    if(enableCounting)
                    {
                        Interlocked.Add(ref totalChildrenProcessed, localFinal.Item2);
                    }
                });

            return totalChildrenProcessed;

            void processNode(Queue<StdMapNode<TKey, TValue>> childrens, StdMapNode<TKey, TValue> current)
            {
                if (!current.IsNil)
                {
                    onEachNotNullNode(current.Data.Key, current.Data.Value);
                }

                // Child pointers are read from a live tree the game mutates concurrently, so a
                // torn read can yield a non-pointer value. Use the non-logging read + validity
                // check and simply stop descending that branch on failure (audit: torn-read noise).
                // Color is the red/black flag and is always 0 or 1 in a real node; if a torn/bad
                // pointer lands us on string or float data, Color is almost never 0/1, so this
                // rejects garbage before we descend into it and propagate the failure further.
                // A leaf's children point at the head node (MSVC's nil sentinel): known without a read, which halved
                // the walk's reads (a town's 250 nodes cost 750 reads a frame, 2026-11-08).
                if (current.Left != nativeContainer.Head &&
                    this.TryReadMemory<StdMapNode<TKey, TValue>>(current.Left, out var leftChild) &&
                    !leftChild.IsNil && leftChild.Color <= 1)
                {
                    childrens.Enqueue(leftChild);
                }

                if (current.Right != nativeContainer.Head &&
                    this.TryReadMemory<StdMapNode<TKey, TValue>>(current.Right, out var rightChild) &&
                    !rightChild.IsNil && rightChild.Color <= 1)
                {
                    childrens.Enqueue(rightChild);
                }
            }

            int processSubTree(Queue<StdMapNode<TKey, TValue>> childrens, StdMapNode<TKey, TValue> subTreeRoot, int forceBreakOnIteration)
            {
                childrens.Enqueue(subTreeRoot);
                var counter = 0;
                while (++counter < forceBreakOnIteration && childrens.TryDequeue(out var current))
                {
                    processNode(childrens, current);
                }

                return counter;
            }
        }

        /// <summary>
        ///     Reads the StdList into a List.
        /// </summary>
        /// <typeparam name="TValue">StdList element structure.</typeparam>
        /// <param name="nativeContainer">native object of the std::list.</param>
        /// <returns>List containing TValue elements.</returns>
        internal List<TValue> ReadStdList<TValue>(StdList nativeContainer)
            where TValue : unmanaged
        {
            const int MaxIterations = 100_000;
            var retList = new List<TValue>();
            var currNodeAddress = this.ReadMemory<StdListNode>(nativeContainer.Head).Next;
            var iterations = 0;
            while (currNodeAddress != nativeContainer.Head)
            {
                if (++iterations > MaxIterations)
                {
                    Console.WriteLine($"[SafeMemoryHandle.ReadStdList] iteration cap {MaxIterations} hit; possible cycle in torn list. Returning partial result.");
                    break;
                }

                var currNode = this.ReadMemory<StdListNode<TValue>>(currNodeAddress);
                if (currNodeAddress == IntPtr.Zero)
                {
                    Console.WriteLine("Terminating reading of list next nodes because of" +
                                      "unexpected 0x00 found. This is normal if it happens " +
                                      "after closing the game, otherwise report it.");
                    break;
                }

                retList.Add(currNode.Data);
                currNodeAddress = currNode.Next;
            }

            return retList;
        }

        /// <summary>
        ///     Reads the std::bucket into a array.
        /// </summary>
        /// <typeparam name="TValue">value type that the std bucket contains.</typeparam>
        /// <param name="nativeContainer">native object of the std::bucket.</param>
        /// <returns>a array containing all the valid values found in std::bucket.</returns>
        internal TValue[] ReadStdBucket<TValue>(StdBucket nativeContainer)
            where TValue : unmanaged
        {
            if (nativeContainer.Data.First == IntPtr.Zero ||
                nativeContainer.Capacity <= 0x00)
            {
                return Array.Empty<TValue>();
            }

            return this.ReadStdVector<TValue>(nativeContainer.Data);
        }

        /// <summary>
        ///     When overridden in a derived class, executes the code required to free the handle.
        /// </summary>
        /// <returns>
        ///     true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false.
        ///     In this case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
        /// </returns>
        protected override bool ReleaseHandle()
        {
            Console.WriteLine($"Releasing handle on 0x{this.handle:X}\n");
            return NativeWrapper.CloseHandle(this.handle);
        }
    }
}
