namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What the scanner knew of each area instance it left, by the instance's hash (AreaInstance.AreaHash, the same after
    ///     a portal to town and back and after a host restart): back through the portal everything returns; after a restart
    ///     the explored ground and the area it was entered from, kept in `config/explored/&lt;hash&gt;.bin`. Both were lost,
    ///     so the whole area was unexplored again ("tengo que seguir buscando todo de nuevo", user 2026-10-29).
    /// </summary>
    public sealed class AreaMemory
    {
        /// <summary>Instances kept whole in memory: a run's last few areas and its town.</summary>
        private const int KeptInMemory = 8;

        /// <summary>Instance files kept; the oldest go (an instance the game has closed is never entered again).</summary>
        private const int KeptOnDisk = 40;

        private const byte FileVersion = 1;
        private const string Extension = ".bin";

        private readonly string directory;
        private readonly PluginLog log;
        private readonly Dictionary<string, Snapshot> left = new(StringComparer.Ordinal);
        private readonly List<string> order = new();

        public AreaMemory(string directory, PluginLog log)
        {
            this.directory = directory;
            this.log = log;
        }

        /// <summary>Remembers what was known of <paramref name="instance"/> as the player leaves it, and writes its explored ground.</summary>
        public void Keep(string instance, Snapshot snapshot)
        {
            this.left[instance] = snapshot;
            this.order.Remove(instance);
            this.order.Add(instance);
            while (this.order.Count > KeptInMemory)
            {
                this.left.Remove(this.order[0]);
                this.order.RemoveAt(0);
            }

            this.Save(instance, snapshot.Explored, snapshot.CameFrom);
        }

        /// <summary>What was known of <paramref name="instance"/>: whole from this session, else its explored ground from disk; null for a new one.</summary>
        public Snapshot? Recall(string instance)
        {
            if (this.left.TryGetValue(instance, out var snapshot))
            {
                return snapshot;
            }

            try
            {
                var path = this.PathOf(instance);
                if (!File.Exists(path))
                {
                    return null;
                }

                using var reader = new BinaryReader(File.OpenRead(path));
                if (reader.ReadByte() != FileVersion)
                {
                    return null;
                }

                var cameFrom = reader.ReadString();
                var explored = reader.ReadBytes(reader.ReadInt32());
                this.log.Info($"Explored ground of instance {instance} restored from disk ({explored.Length} bytes)");
                return new Snapshot(explored, cameFrom, new Dictionary<uint, ScanEntry>(), new HashSet<uint>(), new Dictionary<uint, Vector2>());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
            {
                return null;
            }
        }

        /// <summary>Writes an instance's explored ground (also while playing, so a host restart loses little).</summary>
        public void Save(string instance, byte[] explored, string cameFrom)
        {
            try
            {
                Directory.CreateDirectory(this.directory);
                using (var writer = new BinaryWriter(File.Create(this.PathOf(instance))))
                {
                    writer.Write(FileVersion);
                    writer.Write(cameFrom);
                    writer.Write(explored.Length);
                    writer.Write(explored);
                }

                foreach (var old in new DirectoryInfo(this.directory).GetFiles("*" + Extension).OrderByDescending(file => file.LastWriteTimeUtc).Skip(KeptOnDisk))
                {
                    old.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not written: kept for this session only.
            }
        }

        private string PathOf(string instance) => Path.Join(this.directory, string.Concat(instance.Where(char.IsLetterOrDigit)) + Extension);

        /// <summary>One instance as it was left; the area it was entered from tells the way on from the way back.</summary>
        public sealed record Snapshot(
            byte[] Explored, string CameFrom, Dictionary<uint, ScanEntry> Remembered, HashSet<uint> Sighted, Dictionary<uint, Vector2> ClosedBarriers);
    }
}
