namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using GameOffsets.Natives;
    using GameReader.Utils;
    using Poe2Access;
    using static Poe2Accessibility.Tests.DatColumnSearchTests;
    using static Check;

    /// <summary>
    ///     The rules the audit of 2026-11-10 put in force in the host and the tools: an update that fails leaves a launcher
    ///     and the files it had, a download gone quiet ends, a game-data step is given up on only for its own failures and
    ///     its old file then stays out of the plugin, a torn map stops at its size, a key column is not taken from the
    ///     zero half of the key before it, and the class of a passive override is checked by the known row.
    /// </summary>
    public static class HostAndToolsAuditTests
    {
        private const string Install = "index=1:2\n";
        private const string Launcher = "Poe2Access.exe";
        private const string SharedCause = "bundle index";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AStepGivenUpOnDidNotMakeTheFileInGameData), AStepGivenUpOnDidNotMakeTheFileInGameData),
            (nameof(AnIndexThatWillNotOpenIsNoStepsFailure), AnIndexThatWillNotOpenIsNoStepsFailure),
            (nameof(ADownloadGoneQuietEnds), ADownloadGoneQuietEnds),
            (nameof(ASlowDownloadThatKeepsComingIsTakenWhole), ASlowDownloadThatKeepsComingIsTakenWhole),
            (nameof(AnUpdatePutsThePackageOverTheInstallAndKeepsTheSettings), AnUpdatePutsThePackageOverTheInstallAndKeepsTheSettings),
            (nameof(AFailedUpdateLeavesTheInstallAsItWas), AFailedUpdateLeavesTheInstallAsItWas),
            (nameof(AFileThatCannotBePutBackDoesNotStrandTheLauncherOrTheOthers), AFileThatCannotBePutBackDoesNotStrandTheLauncherOrTheOthers),
            (nameof(ATornLoopInASubtreeLeftUnreadStopsAtTheMapsSize), ATornLoopInASubtreeLeftUnreadStopsAtTheMapsSize),
            (nameof(AKeyIntoAnotherTableIsNotTakenFromTheZeroHalfOfTheKeyBeforeIt), AKeyIntoAnotherTableIsNotTakenFromTheZeroHalfOfTheKeyBeforeIt),
            (nameof(AKeyIntoTheSameTableIsEightBytes), AKeyIntoTheSameTableIsEightBytes),
            (nameof(AnOverrideWhoseClassReadsOtherwiseFailsTheStep), AnOverrideWhoseClassReadsOtherwiseFailsTheStep),
            (nameof(ANumberAtTheTopOfTheRangeIsNoStringAndNoList), ANumberAtTheTopOfTheRangeIsNoStringAndNoList),
        };

        private static void AStepGivenUpOnDidNotMakeTheFileInGameData()
        {
            var stamp = new GameDataStamp(Install, new Dictionary<string, int>()).With(Install, "trial data", 1);
            Expect(true, stamp.Made(Install, "trial data", 1), "a step that worked made its file");
            for (var start = 0; start < GameDataStamp.MaxAttempts; start++)
            {
                stamp = stamp.WithFailure(Install, "trial data", 2);
            }

            Expect(true, stamp.IsCurrent(Install, "trial data", 2), "given up: stamped, so it is left alone");
            Expect(false, stamp.Made(Install, "trial data", 2), "but the file in GameData is version 1's: it stays out of the plugin");
            Expect(false, GameDataStamp.Parse(stamp.ToString()).Made(Install, "trial data", 2), "the same after the stamp is read back");
            Expect(true, stamp.With(Install, "trial data", 2).Made(Install, "trial data", 2), "once it works, it made it");
        }

        private static void AnIndexThatWillNotOpenIsNoStepsFailure()
        {
            var stamp = new GameDataStamp(Install, new Dictionary<string, int>());
            var after = stamp;
            for (var start = 0; start < GameDataStamp.MaxAttempts + 1; start++)
            {
                after = after.After(Install, "hazards", 3, optional: true, SharedCause, SharedCause);
            }

            Expect(true, ReferenceEquals(stamp, after), "the index failing changes no step's stamp");
            Equal(0, after.GivenUp().Count(), "and gives none up");

            var own = stamp.After(Install, "hazards", 3, optional: true, "exit code 1", SharedCause);
            Equal(1, own.Attempts("hazards", 3), "a step's own failure is a failed start");
            Expect(true, ReferenceEquals(stamp, stamp.After(Install, "area names", 1, optional: false, "exit code 1", SharedCause)), "a required step is never counted: it is tried at every start");
            Expect(true, stamp.After(Install, "hazards", 3, optional: true, null, SharedCause).IsCurrent(Install, "hazards", 3), "a step that worked is stamped");
        }

        private static void ADownloadGoneQuietEnds()
        {
            using var into = new MemoryStream();
            var copy = Task.Run(() => PackageInstall.Download(new PausingStream(new byte[100], pieces: 2, pauseMilliseconds: Timeout.Infinite), into, TimeSpan.FromMilliseconds(200)));
            var ended = Task.WaitAny(new Task[] { copy }, TimeSpan.FromSeconds(20)) == 0;
            Expect(true, ended, "the copy ended");
            Expect(true, copy.Exception?.GetBaseException() is TimeoutException, $"with a timeout ({copy.Exception?.GetBaseException().GetType().Name})");
        }

        private static void ASlowDownloadThatKeepsComingIsTakenWhole()
        {
            // Longer in all than the quiet allowed, never quiet that long: a slow line still gets its update.
            var bytes = Enumerable.Range(0, 1000).Select(value => (byte)value).ToArray();
            using var into = new MemoryStream();
            PackageInstall.Download(new PausingStream(bytes, pieces: 30, pauseMilliseconds: 40), into, TimeSpan.FromMilliseconds(800));
            Expect(true, bytes.SequenceEqual(into.ToArray()), "every byte, in order");
        }

        private static void AnUpdatePutsThePackageOverTheInstallAndKeepsTheSettings()
        {
            using var folders = new UpdateFolders();
            folders.Replace();
            Expect("new launcher", folders.Read(Launcher), "the launcher");
            Expect("old launcher", folders.Read(Launcher + PackageInstall.OldSuffix), "the launcher that ran, aside for the next start to delete");
            Expect("new a", folders.Read(@"app\a.txt"), "a file replaced");
            Expect("new e", folders.Read(@"app\e.txt"), "a file added");
            Expect("the player's", folders.Read(@"app\config\settings.txt"), "the settings kept");
            Equal(0, Directory.EnumerateFiles(folders.Root, "*.new", SearchOption.AllDirectories).Count(), "nothing left half way");
        }

        private static void AFailedUpdateLeavesTheInstallAsItWas()
        {
            using var folders = new UpdateFolders();
            using (File.Open(folders.Path(@"app\d.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Expect(true, FailsOnAFile(() => folders.Replace()), "a file in use fails the update");
            }

            Expect("old launcher", folders.Read(Launcher), "the launcher is the one that ran");
            Expect(false, File.Exists(folders.Path(Launcher + PackageInstall.OldSuffix)), "and is not left aside");
            foreach (var name in new[] { "a", "b", "c", "d" })
            {
                Expect($"old {name}", folders.Read($@"app\{name}.txt"), $"{name}.txt as it was");
            }

            Expect(false, File.Exists(folders.Path(@"app\e.txt")), "no file of the new version left");
            Equal(0, folders.Log.Count, "nothing that could not be put back");
        }

        private static void AFileThatCannotBePutBackDoesNotStrandTheLauncherOrTheOthers()
        {
            using var folders = new UpdateFolders();
            FileStream? held = null;
            try
            {
                using (File.Open(folders.Path(@"app\d.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // a.txt is taken (a scanner opens what was just written) once it is replaced and before d.txt fails.
                    bool Kept(string relative)
                    {
                        if (relative == @"app\c.txt")
                        {
                            held = File.Open(folders.Path(@"app\a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
                        }

                        return relative.StartsWith(@"app\config\", StringComparison.OrdinalIgnoreCase);
                    }

                    Expect(true, FailsOnAFile(() => folders.Replace(Kept)), "a file in use fails the update");
                }
            }
            finally
            {
                held?.Dispose();
            }

            Expect("old launcher", folders.Read(Launcher), "the launcher is back whatever else is not");
            Expect("old b", folders.Read(@"app\b.txt"), "the file after the one held is put back");
            Expect("old c", folders.Read(@"app\c.txt"), "and the next");
            Expect("new a", folders.Read(@"app\a.txt"), "the one held stays as the update left it");
            Expect(true, folders.Log.Count == 1 && folders.Log[0].Contains("a.txt", StringComparison.Ordinal), $"and is named in the log ({string.Join(" | ", folders.Log)})");
        }

        private static void ATornLoopInASubtreeLeftUnreadStopsAtTheMapsSize()
        {
            const int Keys = 600;
            const ulong Limit = 400;
            var nodes = new Dictionary<IntPtr, StdMapNode<ulong, int>>();
            var head = new IntPtr(0x20_0000_0000);
            IntPtr Address(int index) => new(0x20_0000_0000 + ((index + 1) * 0x100L));
            var keys = Enumerable.Range(1, Keys).Select(key => (ulong)key).ToArray();
            (keys[4], keys[550]) = (keys[550], keys[4]); // one node out of order: what the limit left unread is read after all
            IntPtr Build(int low, int high)
            {
                if (low > high)
                {
                    return head;
                }

                var middle = (low + high) / 2;
                nodes[Address(middle)] = new StdMapNode<ulong, int>
                {
                    Left = Build(low, middle - 1), Right = Build(middle + 1, high), Color = 1,
                    Data = new StdMapNodeData<ulong, int> { Key = keys[middle], Value = middle },
                };
                return Address(middle);
            }

            var root = Build(0, Keys - 1);
            nodes[head] = new StdMapNode<ulong, int> { Parent = root, Left = head, Right = head, IsNil = true, Color = 1 };

            // The subtree right of the first node at or above the limit is left unread; its smallest leaf leads back to its top.
            var above = nodes[root];
            while (above.Data.Key < Limit)
            {
                above = nodes[above.Right];
            }

            var top = above.Right;
            var leaf = top;
            while (nodes[leaf].Left != head)
            {
                leaf = nodes[leaf].Left;
            }

            nodes[leaf] = nodes[leaf] with { Left = top };

            var reads = 0;
            bool Read(IntPtr address, out StdMapNode<ulong, int> node)
            {
                Interlocked.Increment(ref reads);
                return nodes.TryGetValue(address, out node);
            }

            var walk = StdMapWalk<ulong, int>.Run(new StdMap { Head = head, Size = Keys }, 100000, Read, (_, _) => true, new StdMapKeyLimit<ulong>(key => key, Limit), 4);
            Expect(true, walk.OutOfOrder, "out of order");
            Expect(false, walk.Complete, "a loop is no complete walk");
            Expect(true, walk.Nodes <= Keys + 1, $"no more nodes than the map holds, and one ({walk.Nodes})");
            Expect(true, reads <= 4 * Keys, $"and their reads ({reads})");
        }

        private static void AKeyIntoAnotherTableIsNotTakenFromTheZeroHalfOfTheKeyBeforeIt()
        {
            // The key before it grew from eight bytes to sixteen: the column measured at +8 is at +0x10 now.
            var target = Rows(40, 0x10);
            var builder = new DatTableBuilder(0x28);
            for (var row = 0; row < 12; row++)
            {
                var index = row;
                builder.Row(w => w.WideKey(0, 100 + index).WideKey(0x10, index % 3 == 0 ? -1 : 20 + index).Key(0x20, 7));
            }

            var table = builder.Build();
            var search = new DatColumnSearch();
            Equal(0x10, search.Key(table, "Table.Other", 8, target), "the column, not the zero half at +8 nor a byte into its neighbour");
            Expect(true, search.Report("test"), "found");
        }

        private static void AKeyIntoTheSameTableIsEightBytes()
        {
            // A row of the table itself is eight bytes: what follows it is the next column, not a second half.
            var builder = new DatTableBuilder(0x18);
            for (var row = 0; row < 12; row++)
            {
                var index = row;
                builder.Row(w => w.Text(0, $"row{index}").Key(8, (index + 1) % 12).Key(0x10, 0x0101010101010101));
            }

            var table = builder.Build();
            Equal(8, new DatColumnSearch().Key(table, "Table.Next", 8, table), "found where it is");
        }

        private static void AnOverrideWhoseClassReadsOtherwiseFailsTheStep()
        {
            // Sixteen zero bytes before the three keys: read at +0 every override is the first class's, the Witch's too.
            var classes = new DatTableBuilder(0x20)
                .Row(w => w.Text(0, "Metadata/Characters/Dex/DexFour")).Row(w => w.Text(0, PassiveOverrideColumns.KnownCharacter)).Build();
            const int Replaced = 5, Shown = 25;
            var nodes = new DatTableBuilder(0x10);
            for (var row = 0; row < 40; row++)
            {
                var id = row == Replaced ? PassiveOverrideColumns.KnownReplaced : row == Shown ? PassiveOverrideColumns.KnownShown : $"node{row}";
                nodes.Row(w => w.Text(0, id));
            }

            var overrides = new DatTableBuilder(0x40);
            for (var row = 0; row < 12; row++)
            {
                var index = row;
                overrides.Row(w => w.WideKey(0x10, 1).WideKey(0x20, index == 0 ? Replaced : 6 + index).WideKey(0x30, index == 0 ? Shown : 26 + index));
            }

            Expect(true, PassiveOverrideColumns.Find(overrides.Build(), classes, nodes.Build()) == null, "the Witch's known override read as another class's: the columns are wrong");
        }

        private static void ANumberAtTheTopOfTheRangeIsNoStringAndNoList()
        {
            var table = new DatTableBuilder(0x10).Row(w => w.Key(0, long.MaxValue).Key(8, long.MaxValue)).Row(w => w.Key(0, 1).Key(8, long.MaxValue)).Build();
            Expect<string?>(null, table.StringOrNull(0, 0), "no string");
            Equal(0, table.KeyList(1, 0).Count, "no list");
            Equal(-1L, table.Pool64(long.MaxValue), "nothing of the variable section");
            Expect(false, table.IsFilledString(long.MaxValue), "no filled string");
        }

        private static DatTable Rows(int count, int rowSize)
        {
            var builder = new DatTableBuilder(rowSize);
            for (var row = 0; row < count; row++)
            {
                var index = row;
                builder.Row(w => w.Text(0, $"row{index}"));
            }

            return builder.Build();
        }

        /// <summary>Whether the file work failed as Windows fails it for a file in use: a sharing violation or an access denied.</summary>
        private static bool FailsOnAFile(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        /// <summary>An installation and a package of the next version in a folder of their own, deleted with this.</summary>
        private sealed class UpdateFolders : IDisposable
        {
            private readonly string package;
            private readonly string backup;

            public UpdateFolders()
            {
                var work = System.IO.Path.Join(System.IO.Path.GetTempPath(), "Poe2AccessTests-" + Guid.NewGuid().ToString("N"));
                (this.Root, this.package, this.backup) = (System.IO.Path.Join(work, "install"), System.IO.Path.Join(work, "package"), System.IO.Path.Join(work, "backup"));
                this.Work = work;
                Write(this.Root, Launcher, "old launcher");
                Write(this.package, Launcher, "new launcher");
                foreach (var name in new[] { "a", "b", "c", "d" })
                {
                    Write(this.Root, $@"app\{name}.txt", $"old {name}");
                    Write(this.package, $@"app\{name}.txt", $"new {name}");
                }

                Write(this.package, @"app\e.txt", "new e");
                Write(this.Root, @"app\config\settings.txt", "the player's");
                Write(this.package, @"app\config\settings.txt", "the package's");
            }

            public string Root { get; }

            public List<string> Log { get; } = new();

            private string Work { get; }

            public string Path(string relative) => System.IO.Path.Join(this.Root, relative);

            public string Read(string relative) => File.ReadAllText(this.Path(relative));

            public void Replace(Func<string, bool>? isKept = null) => PackageInstall.Replace(
                this.package, this.Root, this.backup, this.Path(Launcher), isKept ?? (relative => relative.StartsWith(@"app\config\", StringComparison.OrdinalIgnoreCase)), this.Log.Add);

            public void Dispose() => Directory.Delete(this.Work, true);

            private static void Write(string root, string relative, string text)
            {
                var path = System.IO.Path.Join(root, relative);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
        }

        /// <summary>A download that arrives in pieces with a pause before each; an endless pause is a connection gone quiet.</summary>
        private sealed class PausingStream(byte[] bytes, int pieces, int pauseMilliseconds) : Stream
        {
            private int position;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => bytes.Length;

            public override long Position { get => this.position; set => throw new NotSupportedException(); }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (this.position >= bytes.Length)
                {
                    return 0;
                }

                await Task.Delay(pauseMilliseconds, cancellationToken).ConfigureAwait(false);
                var count = Math.Min(Math.Min(buffer.Length, bytes.Length - this.position), Math.Max(1, bytes.Length / pieces));
                bytes.AsMemory(this.position, count).CopyTo(buffer);
                this.position += count;
                return count;
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
