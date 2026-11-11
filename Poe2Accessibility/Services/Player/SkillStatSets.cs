namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The stats a player's skills carry at their gem's level (GrantedEffectStatSetsPerLevel's additional stats), by Stats
    ///     name: a maximum the player's own stats do not hold. Rapid Shot's maximum Heat is one: the player stat reads 0, and the
    ///     table's row for the gem's level lists "maximum_number_of_crossbow_heat_stacks" at 25 + the level (30 at level 5, as
    ///     the gem's text says; read from the dat files 2026-11-08). Every step is checked by the table names its keys lead to and
    ///     by the two lists pairing; a step that does not read gives nothing, never a guess.
    /// </summary>
    public sealed class SkillStatSets
    {
        public const string Table = "Data/Balance/GrantedEffectStatSetsPerLevel.dat";

        public const string StatsTable = "Data/Balance/Stats.dat";

        /// <summary>
        ///     The level row's additional stats: (count, pointer) to 16-byte keys into Stats.dat, and (count, pointer) to their
        ///     int32 values. Offsets of the dat file (rows of 0xB9 bytes; Rapid Shot's set, rows 8685-8724, one per level), which
        ///     a loaded row keeps (PassiveSkills' unaligned columns read so: <see cref="GameLayout.PassiveTree"/>).
        /// </summary>
        public const int StatKeysOffset = 0x81;

        public const int ValuesOffset = 0x2C;

        /// <summary>Where in an ActiveSkillDetails its stat set's key is looked for (8-byte steps; not measured live yet).</summary>
        private const int DetailsSearchBytes = 0x100;

        private const int KeyBytes = 16;
        private const int MaxStats = 64;
        private const int MaxSkills = 256;
        private const int MaxNameChars = 96;
        private const int RefreshMs = 2000;

        private readonly Func<Entity?> subject;
        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly PluginLog? log;
        private readonly Dictionary<string, (long At, int? Value)> cache = new(StringComparer.Ordinal);
        private int? keyOffset;
        private bool reported;

        public SkillStatSets(Func<Entity?> subject, HostMemoryReader memory, UiElementReader elements, PluginLog? log)
        {
            this.subject = subject;
            this.memory = memory;
            this.elements = elements;
            this.log = log;
        }

        /// <summary>The largest value any of the subject's skills gives the first of <paramref name="names"/> it lists; null if none.</summary>
        public int? Value(IEnumerable<string> names)
        {
            var list = names.ToList();
            var cacheKey = string.Join("|", list);
            var now = Environment.TickCount64;
            if (this.cache.TryGetValue(cacheKey, out var known) && now - known.At < RefreshMs)
            {
                return known.Value;
            }

            var value = this.Read(list);
            this.cache[cacheKey] = (now, value);
            return value;
        }

        private int? Read(IReadOnlyList<string> names)
        {
            if (this.subject() is not { } entity || !entity.TryGetComponent<Actor>(out var actor))
            {
                return null;
            }

            var skills = actor.Address + GameLayout.ActorComponent.SkillsOffset;
            if (!this.memory.TryRead<IntPtr>(skills, out var first) || !this.memory.TryRead<IntPtr>(skills + IntPtr.Size, out var last) || first == IntPtr.Zero)
            {
                return null;
            }

            int? best = null;
            var count = Math.Min(MaxSkills, (last.ToInt64() - first.ToInt64()) / GameLayout.ActorComponent.SkillEntrySize);
            for (var i = 0; i < count; i++)
            {
                if (this.memory.TryRead<IntPtr>(first + (int)(i * GameLayout.ActorComponent.SkillEntrySize), out var details) && details != IntPtr.Zero &&
                    this.LevelRow(details) is { } row && this.StatValue(row, names) is { } value && (best == null || value > best))
                {
                    best = value;
                }
            }

            return best;
        }

        /// <summary>The skill's GrantedEffectStatSetsPerLevel row: its key found once among the details' 8-byte steps by the table it names.</summary>
        private IntPtr? LevelRow(IntPtr details)
        {
            // A known offset that no longer names the table (a patch moved it under a host that outlived the game process)
            // falls through to the search again and is replaced; a skill with no stat set just finds nothing, as before.
            if (this.keyOffset is { } known && this.Names(details + known, Table))
            {
                return this.Row(details + known);
            }

            for (var offset = 0; offset < DetailsSearchBytes; offset += IntPtr.Size)
            {
                if (this.Names(details + offset, Table))
                {
                    this.keyOffset = offset;
                    this.log?.Info($"Skill stat sets: the level's stat set key at ActiveSkillDetails+0x{offset:X2}");
                    return this.Row(details + offset);
                }
            }

            return null;
        }

        private IntPtr? Row(IntPtr key) => this.memory.TryRead<IntPtr>(key, out var row) && row != IntPtr.Zero ? row : null;

        /// <summary>The key's file is the named table (<see cref="DatKey"/>'s layout).</summary>
        private bool Names(IntPtr key, string table) =>
            this.memory.TryRead<IntPtr>(key, out var row) && row != IntPtr.Zero &&
            this.memory.TryRead<IntPtr>(key + DatKey.FileOffset, out var file) && file != IntPtr.Zero &&
            string.Equals(this.elements.ReadWStringStructAt(file + DatKey.NameOffset), table, StringComparison.OrdinalIgnoreCase);

        private int? StatValue(IntPtr row, IReadOnlyList<string> names)
        {
            if (!this.memory.TryRead<long>(row + StatKeysOffset, out var keyCount) || !this.memory.TryRead<IntPtr>(row + StatKeysOffset + sizeof(long), out var keys) ||
                !this.memory.TryRead<long>(row + ValuesOffset, out var valueCount) || !this.memory.TryRead<IntPtr>(row + ValuesOffset + sizeof(long), out var values))
            {
                return null;
            }

            if (keyCount == 0 && valueCount == 0)
            {
                return null; // a level with no additional stats (most skills): nothing to find, nothing wrong
            }

            if (keyCount != valueCount || keyCount is < 0 or > MaxStats || keys == IntPtr.Zero || values == IntPtr.Zero)
            {
                this.Report($"the level row's stat lists do not pair (+0x{StatKeysOffset:X2} and +0x{ValuesOffset:X2}): columns moved?");
                return null;
            }

            var stats = new List<string?>();
            for (var i = 0; i < keyCount; i++)
            {
                var key = keys + (i * KeyBytes);
                if (!this.Names(key, StatsTable))
                {
                    this.Report($"the level row's +0x{StatKeysOffset:X2} list is not of {StatsTable}: columns moved?");
                    return null;
                }

                stats.Add(this.Row(key) is { } stat && this.memory.TryRead<IntPtr>(stat, out var id) ? this.elements.ReadUtf16BufferAt(id, MaxNameChars) : null);
            }

            var read = this.memory.ReadArray<int>(values, (int)valueCount);
            return read.Length == valueCount ? ClassMechanicCatalog.StatValue(stats, read, names) : null;
        }

        private void Report(string what)
        {
            if (!this.reported)
            {
                this.reported = true;
                this.log?.Info($"Skill stat sets: {what}");
            }
        }
    }
}
