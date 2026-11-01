namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     What the scanner knew of each area instance it left: explored ground, remembered places, the way in. An instance
    ///     is its hash (AreaInstance.AreaHash) and its layout (<see cref="Layout"/>); a regenerated map has another layout and
    ///     starts unexplored (the D2R mod keys by seed, level and room layout). Kept whole on disk, `config/explored/`.
    /// </summary>
    public sealed class AreaMemory
    {
        /// <summary>Instances kept whole in memory: a run's last few areas and its town.</summary>
        private const int KeptInMemory = 8;

        /// <summary>Instance files kept; the oldest go (an instance the game has closed is never entered again).</summary>
        private const int KeptOnDisk = 40;

        private const byte FileVersion = 2;
        private const string Extension = ".bin";
        private const ulong FnvOffset = 14695981039346656037;
        private const ulong FnvPrime = 1099511628211;

        private readonly string directory;
        private readonly PluginLog log;
        private readonly Dictionary<string, Snapshot> left = new(StringComparer.Ordinal);
        private readonly List<string> order = new();

        public AreaMemory(string directory, PluginLog log)
        {
            this.directory = directory;
            this.log = log;
        }

        /// <summary>The map's fingerprint: its tiles (name and grid spot, in a fixed order) and grid size; doors and barriers leave it alone.</summary>
        public static ulong Layout(IReadOnlyDictionary<string, List<Vector2>> tiles, int bytesPerRow, int gridBytes)
        {
            var hash = Mix(Mix(FnvOffset, (uint)bytesPerRow), (uint)gridBytes);
            foreach (var name in tiles.Keys.OrderBy(name => name, StringComparer.Ordinal))
            {
                foreach (var c in name)
                {
                    hash = Mix(hash, c);
                }

                foreach (var spot in tiles[name].OrderBy(spot => spot.X).ThenBy(spot => spot.Y))
                {
                    hash = Mix(Mix(hash, (uint)(int)spot.X), (uint)(int)spot.Y);
                }
            }

            return hash;

            static ulong Mix(ulong hash, uint value)
            {
                for (var i = 0; i < 4; i++, value >>= 8)
                {
                    hash = (hash ^ (value & 0xFF)) * FnvPrime;
                }

                return hash;
            }
        }

        /// <summary>Remembers what was known of <paramref name="instance"/> as the player leaves it, and writes it.</summary>
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

            this.Save(instance, snapshot);
        }

        /// <summary>What was known of <paramref name="instance"/> if its map is still <paramref name="layout"/>; null for a new or regenerated one.</summary>
        public Snapshot? Recall(string instance, ulong layout)
        {
            var known = this.left.TryGetValue(instance, out var kept) ? kept : this.Load(instance);
            if (known == null)
            {
                return null;
            }

            if (known.Layout != layout)
            {
                this.log.Info($"Instance {instance}: the map was regenerated, its old exploration dropped");
                this.left.Remove(instance);
                this.order.Remove(instance);
                this.Delete(instance);
                return null;
            }

            return known;
        }

        /// <summary>Writes an instance's snapshot (also while playing, so a host restart loses little).</summary>
        public void Save(string instance, Snapshot snapshot)
        {
            try
            {
                Directory.CreateDirectory(this.directory);
                using (var writer = new BinaryWriter(File.Create(this.PathOf(instance))))
                {
                    Write(writer, snapshot);
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

        private static void Write(BinaryWriter writer, Snapshot snapshot)
        {
            writer.Write(FileVersion);
            writer.Write(snapshot.Layout);
            writer.Write(snapshot.CameFrom);
            writer.Write(snapshot.Explored.Length);
            writer.Write(snapshot.Explored);
            writer.Write(snapshot.Remembered.Count);
            foreach (var entry in snapshot.Remembered.Values)
            {
                writer.Write(entry.Id);
                writer.Write((int)entry.Category);
                writer.Write(entry.Name);
                writer.Write(entry.WorldX);
                writer.Write(entry.WorldY);
                writer.Write(entry.WorldZ);
                writer.Write(entry.Kind);
                writer.Write(entry.BaseId);
                writer.Write((int)entry.Loot);
                writer.Write(entry.Sound);
                writer.Write(entry.QuestMarked);
                writer.Write(entry.Subject);
            }

            writer.Write(snapshot.Sighted.Count);
            foreach (var id in snapshot.Sighted)
            {
                writer.Write(id);
            }

            writer.Write(snapshot.ClosedBarriers.Count);
            foreach (var (id, at) in snapshot.ClosedBarriers)
            {
                writer.Write(id);
                writer.Write(at.X);
                writer.Write(at.Y);
            }
        }

        private static Snapshot? Read(BinaryReader reader)
        {
            if (reader.ReadByte() != FileVersion)
            {
                return null;
            }

            var layout = reader.ReadUInt64();
            var cameFrom = reader.ReadString();
            var explored = reader.ReadBytes(reader.ReadInt32());
            var remembered = new Dictionary<uint, ScanEntry>();
            for (var i = reader.ReadInt32(); i > 0; i--)
            {
                var entry = new ScanEntry(
                    reader.ReadUInt32(), (ScanCategory)reader.ReadInt32(), reader.ReadString(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                    reader.ReadString(), reader.ReadString(), (LootTier)reader.ReadInt32(), reader.ReadString(), reader.ReadBoolean(), reader.ReadString());
                remembered[entry.Id] = entry;
            }

            var sighted = new HashSet<uint>();
            for (var i = reader.ReadInt32(); i > 0; i--)
            {
                sighted.Add(reader.ReadUInt32());
            }

            var barriers = new Dictionary<uint, Vector2>();
            for (var i = reader.ReadInt32(); i > 0; i--)
            {
                barriers[reader.ReadUInt32()] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }

            return new Snapshot(layout, explored, cameFrom, remembered, sighted, barriers);
        }

        private Snapshot? Load(string instance)
        {
            try
            {
                var path = this.PathOf(instance);
                if (!File.Exists(path))
                {
                    return null;
                }

                using var reader = new BinaryReader(File.OpenRead(path));
                return Read(reader);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
            {
                return null;
            }
        }

        private void Delete(string instance)
        {
            try
            {
                File.Delete(this.PathOf(instance));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left on disk: its layout no longer matches, so it is never restored.
            }
        }

        private string PathOf(string instance) => Path.Join(this.directory, string.Concat(instance.Where(char.IsLetterOrDigit)) + Extension);

        /// <summary>One instance as it was left; the area it was entered from tells the way on from the way back.</summary>
        public sealed record Snapshot(
            ulong Layout, byte[] Explored, string CameFrom, Dictionary<uint, ScanEntry> Remembered, HashSet<uint> Sighted, Dictionary<uint, Vector2> ClosedBarriers);
    }
}
