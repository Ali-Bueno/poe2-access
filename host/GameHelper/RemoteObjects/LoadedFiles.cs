// <copyright file="LoadedFiles.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects;
    using Utils;

    /// <summary>
    ///     Gathers the files loaded in the game for the current area.
    /// </summary>
    public class LoadedFiles : RemoteObjectBase
    {
        private string areaHashCache = string.Empty;
        private string filename = string.Empty;
        private string searchText = string.Empty;
        private string[] searchTextSplit = Array.Empty<string>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="LoadedFiles" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal LoadedFiles(IntPtr address)
            : base(address)
        {
            Core.CoroutinesRegistrar.Add(CoroutineHandler.Start(
                this.OnAreaChange(), "[LoadedFiles] Gather Preload Data", int.MaxValue - 1));
        }

        /// <summary>
        ///     Gets the pathname of the files.
        /// </summary>
        public ConcurrentDictionary<string, int> PathNames { get; }

            = new();

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.PathNames.Clear();
            this.areaHashCache = string.Empty;
            this.filename = string.Empty;
        }

        /// <summary>
        ///     this function is overrided to do nothing because it's done @ AreaChange event.
        /// </summary>
        /// <param name="hasAddressChanged">ignore me.</param>
        protected override void UpdateData(bool hasAddressChanged) { }

        private LoadedFilesRootObject[] GetAllPointers()
        {
            var totalFiles = LoadedFilesRootObject.TotalCount;
            var reader = Core.Process.Handle;
            return reader.ReadMemoryArray<LoadedFilesRootObject>(
                reader.ReadMemory<IntPtr>(this.Address), totalFiles);
        }

        private void ScanForFilesParallel(SafeMemoryHandle reader, LoadedFilesRootObject filesRootObj)
        {
            var filesPtr = reader.ReadStdBucket<FilesPointerStructure>(filesRootObj.LoadedFiles);
            Parallel.ForEach(filesPtr, fileNode =>
            {
                this.AddFileIfLoadedInCurrentArea(reader, fileNode.FilesPointer);
            });
        }

        private void AddFileIfLoadedInCurrentArea(SafeMemoryHandle reader, IntPtr address)
        {
            var information = reader.ReadMemory<FileInfoValueStruct>(address);
            if (information.AreaChangeCount > FileInfoValueStruct.IGNORE_FIRST_X_AREAS &&
                information.AreaChangeCount == Core.AreaChangeCounter.Value)
            {
                var name = reader.ReadStdWString(information.Name).Split('@')[0];
                this.PathNames.AddOrUpdate(name, information.AreaChangeCount,
                    (key, oldValue) => { return Math.Max(oldValue, information.AreaChangeCount); });
            }
        }

        private IEnumerator<Wait> OnAreaChange()
        {
            while (true)
            {
                yield return new Wait(RemoteEvents.AreaChanged);
                if (this.Address == IntPtr.Zero)
                {
                    continue;
                }

                LoadedFilesRootObject[] filesRootObjs;
                SafeMemoryHandle reader;
                try
                {
                    var areaHash = Core.States.InGameStateObject.CurrentAreaInstance.AreaHash;
                    var iH = Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails.IsHideout;
                    var iT = Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails.IsTown;
                    var name = Core.States.AreaLoading.CurrentAreaName;
                    if ((iH && Core.GHSettings.SkipPreloadedFilesInHideout) || iT || areaHash == this.areaHashCache)
                    {
                        continue;
                    }

                    this.CleanUpData();
                    this.filename = $"{name}_{areaHash}.txt";
                    this.areaHashCache = areaHash;

                    filesRootObjs = this.GetAllPointers();
                    reader = Core.Process.Handle;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LoadedFiles.OnAreaChange.setup] {ex}");
                    continue;
                }

                for (var i = 0; i < filesRootObjs.Length; i++)
                {
                    try
                    {
                        this.ScanForFilesParallel(reader, filesRootObjs[i]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LoadedFiles.OnAreaChange.scan] {ex}");
                    }

                    yield return new Wait(0d);
                }

                try
                {
                    CoroutineHandler.RaiseEvent(HybridEvents.PreloadsUpdated);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LoadedFiles.OnAreaChange.raise] {ex}");
                }
            }
        }
    }
}