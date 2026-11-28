namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;

    /// <summary>
    ///     The client's rulesets (<see cref="GameLayout.Rulesets"/>): one object per row of Rulesets.dat that the server keeps told,
    ///     where the Breach's ring, the Expedition's charges and the Trial of Chaos' round live. Found from the exe alone, so every
    ///     step is checked against something already known before it is believed: the session by the area it points back to, a
    ///     ruleset by the name of its row and by the session it points back to, each number by its range.
    /// </summary>
    public sealed class RulesetReader
    {
        /// <summary>A session not found is looked for again this often: the search reads a few hundred pointers.</summary>
        private const int RetryMs = 5000;

        private const int MaxNameChars = 64;

        /// <summary>More records than this in one vector says the pointers are not a vector's.</summary>
        private const int MaxRecords = 64;

        /// <summary>The records of the last vector refused for being too many, 0 when none was.</summary>
        private long refusedRecords;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private IntPtr session;
        private IntPtr area;
        private long lastSearch = long.MinValue / 2;
        private bool missLogged;

        public RulesetReader(HostMemoryReader memory, UiElementReader elements, PluginLog log)
        {
            this.memory = memory;
            this.elements = elements;
            this.log = log;
        }

        /// <summary>A new area: its session and rulesets are found afresh.</summary>
        public void Reset()
        {
            (this.session, this.area, this.lastSearch) = (IntPtr.Zero, IntPtr.Zero, long.MinValue / 2);
        }

        /// <summary>The ring of the Breach nearest a place, while one is open; null without one or when its numbers cannot be a ring's.</summary>
        public BreachRing? BreachNear(Vector2 playerGrid)
        {
            BreachRing? nearest = null;
            foreach (var record in this.Records(GameLayout.Rulesets.BreachName, GameLayout.Rulesets.BreachEncounters, GameLayout.Rulesets.BreachStride))
            {
                if (!this.memory.TryRead<float>(record + GameLayout.Rulesets.BreachFrom, out var from) ||
                    !this.memory.TryRead<float>(record + GameLayout.Rulesets.BreachTo, out var to) ||
                    !this.memory.TryRead<float>(record + GameLayout.Rulesets.BreachDuration, out var duration) ||
                    !this.memory.TryRead<float>(record + GameLayout.Rulesets.BreachElapsed, out var elapsed) ||
                    !this.memory.TryRead<int>(record + GameLayout.Rulesets.BreachCentre, out var x) ||
                    !this.memory.TryRead<int>(record + GameLayout.Rulesets.BreachCentre + sizeof(int), out var y) ||
                    !this.memory.TryRead<byte>(record + GameLayout.Rulesets.BreachState, out var state) ||
                    BreachRings.ValueOf(from, to, duration, elapsed) is not { } value || state is not (1 or 2) || !IsGridCell(x, y))
                {
                    continue; // unread, or a ring not growing: a record kept after its Breach closed is no ring to stand in
                }

                var ring = new BreachRing(new Vector2(x, y), value, state);
                if (nearest is not { } best || Vector2.Distance(ring.Centre, playerGrid) < Vector2.Distance(best.Centre, playerGrid))
                {
                    nearest = ring;
                }
            }

            return nearest;
        }

        /// <summary>The charges of the Expedition site nearest a place: how many are placed and how many there are; null without one.</summary>
        public (int Placed, int Total)? ExpeditionNear(Vector2 playerGrid)
        {
            (int Placed, int Total)? nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var record in this.Records(GameLayout.Rulesets.ExpeditionName, GameLayout.Rulesets.ExpeditionSites, GameLayout.Rulesets.ExpeditionStride))
            {
                if (!this.memory.TryRead<byte>(record + GameLayout.Rulesets.ExpeditionTotal, out var total) || total is 0 or > GameLayout.Rulesets.ExpeditionMostCharges ||
                    !this.memory.TryRead<int>(record + GameLayout.Rulesets.ExpeditionPlace, out var x) ||
                    !this.memory.TryRead<int>(record + GameLayout.Rulesets.ExpeditionPlace + sizeof(int), out var y) || !IsGridCell(x, y) ||
                    this.VectorCount(record + GameLayout.Rulesets.ExpeditionPlaced, GameLayout.Rulesets.ExpeditionChargeBytes) is not { } placed || placed > total)
                {
                    continue;
                }

                var distance = Vector2.Distance(new Vector2(x, y), playerGrid);
                if (distance < nearestDistance)
                {
                    (nearest, nearestDistance) = ((placed, total), distance);
                }
            }

            return nearest;
        }

        /// <summary>The Trial of Chaos' round, counted from 1, and how many it has; null outside one or when the two bytes cannot be that.</summary>
        public (int Round, int Total)? TrialRound()
        {
            if (this.Find(GameLayout.Rulesets.UltimatumName) is not { } rules ||
                !this.memory.TryRead<byte>(rules + GameLayout.Rulesets.UltimatumRound, out var round) ||
                !this.memory.TryRead<byte>(rules + GameLayout.Rulesets.UltimatumTotal, out var total) ||
                total is 0 or > GameLayout.Rulesets.UltimatumMostRounds || round >= total)
            {
                return null;
            }

            return (round + 1, total);
        }

        /// <summary>
        ///     The Trial of Chaos' round as the game names it: the kind's Id ("Survive") and the objective in the client's language
        ///     ("Sobrevive"); null outside one, or when the pointers do not lead to a kind with a plain Id and a text.
        /// </summary>
        public (IntPtr Room, string Kind, string Text)? TrialObjective()
        {
            if (this.Find(GameLayout.Rulesets.UltimatumName) is not { } rules ||
                !this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.UltimatumRoom, out var room) || !this.memory.IsValidAddress(room) ||
                !this.memory.TryRead<IntPtr>(room + GameLayout.Rulesets.UltimatumRoomType, out var type) || !this.memory.IsValidAddress(type) ||
                !this.memory.TryRead<IntPtr>(type, out var idText) || !this.memory.IsValidAddress(idText) ||
                !this.memory.TryRead<IntPtr>(type + GameLayout.Rulesets.UltimatumTypeText, out var nameText) || !this.memory.IsValidAddress(nameText) ||
                this.elements.ReadUtf16BufferAt(idText, MaxNameChars) is not { Length: > 0 } kind || !kind.All(char.IsAsciiLetterOrDigit) ||
                this.elements.ReadUtf16BufferAt(nameText, MaxTextChars) is not { Length: > 0 } text || text.Any(char.IsControl))
            {
                return null;
            }

            return (room, kind, text);
        }

        private const int MaxTextChars = 120;

        /// <summary>How full the Trial's bar "Challenge Progress" is, 0 to 1; null outside a Trial or when the float cannot be that.</summary>
        public float? TrialProgress() =>
            this.Find(GameLayout.Rulesets.UltimatumName) != null &&
            this.memory.TryRead<float>(this.session + GameLayout.Rulesets.SessionTrialProgress, out var progress) && float.IsFinite(progress) && progress is >= 0f and <= 1f
                ? progress
                : null;

        /// <summary>Research: what was found, for the log and the dev command: the session and the area's rulesets by name.</summary>
        public string Describe()
        {
            if (this.session == IntPtr.Zero)
            {
                return "Rulesets: session not found";
            }

            var present = new List<string>();
            for (var row = 0; row < GameLayout.Rulesets.MostRows; row++)
            {
                if (this.RulesetAt(this.session, row) != null)
                {
                    present.Add(GameIds.Current.RulesetName(row) ?? $"row {row}");
                }
            }

            return $"Rulesets: session 0x{this.session.ToInt64():X}, {present.Count} of this area: {string.Join(", ", present)}";
        }

        /// <summary>
        ///     Research (dev command "rulesets"): where the session is from the game state and from the area, by what a session
        ///     must hold: rulesets that point back to it (+0x48 of each pointer of its array), and the area somewhere near +0x20F0.
        ///     One and two pointers deep; reads tens of thousands of pointers, so only when asked.
        /// </summary>
        public IEnumerable<string> Probe()
        {
            var state = Core.States.InGameStateObject;
            var areaNow = state.CurrentAreaInstance.Address;
            yield return $"Rulesets probe: game state 0x{state.Address.ToInt64():X}, area 0x{areaNow.ToInt64():X}";
            foreach (var (label, root) in new[] { ("game state", state.Address), ("area", areaNow) })
            {
                var block = this.memory.ReadBytes(root, GameLayout.Rulesets.GameStateSearchBytes);
                for (var offset = 0; offset + IntPtr.Size <= block.Length; offset += IntPtr.Size)
                {
                    var first = new IntPtr(BitConverter.ToInt64(block, offset));
                    if (first == IntPtr.Zero || !this.memory.IsValidAddress(first))
                    {
                        continue;
                    }

                    if (this.SessionLike(first, areaNow) is { } direct)
                    {
                        yield return $"Rulesets probe: {label} +0x{offset:X} -> 0x{first.ToInt64():X}: {direct}";
                    }

                    var inner = this.memory.ReadBytes(first, ProbeInnerBytes);
                    for (var deeper = 0; deeper + IntPtr.Size <= inner.Length; deeper += IntPtr.Size)
                    {
                        var second = new IntPtr(BitConverter.ToInt64(inner, deeper));
                        if (second != IntPtr.Zero && second != first && this.memory.IsValidAddress(second) && this.SessionLike(second, areaNow) is { } found)
                        {
                            yield return $"Rulesets probe: {label} +0x{offset:X} -> +0x{deeper:X} -> 0x{second.ToInt64():X}: {found}";
                        }
                    }
                }
            }

            yield return "Rulesets probe: done";
        }

        private const int ProbeInnerBytes = 0x400;
        private const int ProbeAreaSpan = 0x200;

        /// <summary>What makes a pointer a session, or null: how many of its rulesets point back to it, and where it holds the area.</summary>
        private string? SessionLike(IntPtr candidate, IntPtr areaNow)
        {
            var array = this.memory.ReadBytes(candidate + GameLayout.Rulesets.SessionRulesets, GameLayout.Rulesets.MostRows * IntPtr.Size);
            var names = new List<string>();
            for (var row = 0; row * IntPtr.Size + IntPtr.Size <= array.Length; row++)
            {
                var rules = new IntPtr(BitConverter.ToInt64(array, row * IntPtr.Size));
                if (rules != IntPtr.Zero && this.memory.IsValidAddress(rules) &&
                    this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.RulesetSession, out var owner) && owner == candidate)
                {
                    var name = this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.RulesetRow, out var datRow) && this.memory.IsValidAddress(datRow) &&
                               this.memory.TryRead<IntPtr>(datRow, out var text) && this.memory.IsValidAddress(text)
                        ? this.elements.ReadUtf16BufferAt(text, MaxNameChars) ?? "?"
                        : "?";
                    names.Add($"{row}={name}");
                }
            }

            var around = this.memory.ReadBytes(candidate + GameLayout.Rulesets.SessionArea - (ProbeAreaSpan / 2), ProbeAreaSpan);
            var areaAt = new List<string>();
            for (var offset = 0; offset + IntPtr.Size <= around.Length; offset += IntPtr.Size)
            {
                if (areaNow != IntPtr.Zero && BitConverter.ToInt64(around, offset) == areaNow.ToInt64())
                {
                    areaAt.Add($"+0x{GameLayout.Rulesets.SessionArea - (ProbeAreaSpan / 2) + offset:X}");
                }
            }

            return names.Count == 0 && areaAt.Count == 0
                ? null
                : $"{names.Count} rulesets point back ({string.Join(", ", names.Take(12))}); area held at {(areaAt.Count == 0 ? "nowhere near" : string.Join(", ", areaAt))}";
        }

        /// <summary>
        ///     The area's stats as upstream's layout places them (<see cref="GameLayout.AreaStats"/>), named by the game's keys where
        ///     the catalog lists them; "unread" when the two pointers are not a vector of pairs. For the log, once per area.
        /// </summary>
        public string AreaStats()
        {
            this.refusedRecords = 0;
            if (this.AreaStatPairs() is not { } pairs)
            {
                return this.refusedRecords > 0 ? $"Area stats: unread, a vector of {this.refusedRecords} pairs is over the {GameLayout.AreaStats.MostPairs} read" : "Area stats: unread";
            }

            // By the game's own key where the catalog lists the stat (game-ids.txt), else by the host's old enum, which drifts.
            var named = pairs.Select(pair => $"{GameIds.Current.StatName(pair.Stat) ?? ((GameReader.RemoteEnums.GameStats)pair.Stat).ToString()} ({pair.Stat}) = {pair.Value}");
            return $"Area stats: {pairs.Count} pairs{(pairs.Count > 0 ? ": " + string.Join(", ", named) : string.Empty)}";
        }

        /// <summary>The value of one of the area's stats by its key; 0 when the area lacks it, null when the stats do not read.</summary>
        public int? AreaStat(int key) => this.AreaStatPairs() is { } pairs ? pairs.FirstOrDefault(pair => pair.Stat == key).Value : null;

        private List<(int Stat, int Value)>? AreaStatPairs()
        {
            var areaNow = Core.States.InGameStateObject.CurrentAreaInstance.Address;
            if (areaNow == IntPtr.Zero || this.VectorCount(areaNow + GameLayout.AreaStats.VectorOffset, GameLayout.AreaStats.PairBytes, GameLayout.AreaStats.MostPairs) is not { } count ||
                !this.memory.TryRead<IntPtr>(areaNow + GameLayout.AreaStats.VectorOffset, out var first))
            {
                return null;
            }

            var pairs = new List<(int Stat, int Value)>();
            for (var index = 0; index < count; index++)
            {
                if (this.memory.TryRead<int>(first + (index * GameLayout.AreaStats.PairBytes), out var stat) &&
                    this.memory.TryRead<int>(first + (index * GameLayout.AreaStats.PairBytes) + sizeof(int), out var value))
                {
                    pairs.Add((stat, value));
                }
            }

            return pairs;
        }

        private static bool IsGridCell(int x, int y) => x is >= 0 and < GameLayout.Rulesets.MostGridCells && y is >= 0 and < GameLayout.Rulesets.MostGridCells;

        /// <summary>The records of a ruleset's vector; none when the ruleset or the vector does not read as one.</summary>
        private IEnumerable<IntPtr> Records(string ruleset, int vectorOffset, int stride)
        {
            if (this.Find(ruleset) is not { } rules || this.VectorCount(rules + vectorOffset, stride) is not { } count ||
                !this.memory.TryRead<IntPtr>(rules + vectorOffset, out var first))
            {
                yield break;
            }

            for (var index = 0; index < count; index++)
            {
                yield return first + (index * stride);
            }
        }

        /// <summary>How many elements a std::vector holds; null when its two pointers are not a vector of that stride.</summary>
        private int? VectorCount(IntPtr vector, int stride, int most = MaxRecords)
        {
            if (!this.memory.TryRead<IntPtr>(vector, out var first) || !this.memory.TryRead<IntPtr>(vector + IntPtr.Size, out var last))
            {
                return null;
            }

            var bytes = last.ToInt64() - first.ToInt64();
            if (bytes == 0)
            {
                return 0;
            }

            if (bytes < 0 || bytes % stride != 0 || !this.memory.IsValidAddress(first))
            {
                return null;
            }

            if (bytes / stride > most)
            {
                this.refusedRecords = bytes / stride;
                return null;
            }

            return (int)(bytes / stride);
        }

        /// <summary>A ruleset of the area by its name, for a reader of what it holds; null when the area has none.</summary>
        public IntPtr? Ruleset(string name) => this.Find(name);

        /// <summary>
        ///     A ruleset by the name of its Rulesets.dat row: the row's number comes from the game's files (catalog/game-ids.txt),
        ///     else from what the exe of 0.5.5f had, and the object at that place of the session's array must point back to the
        ///     session. Null when the area has none.
        /// </summary>
        private IntPtr? Find(string name)
        {
            if (!this.EnsureSession() || RowOf(name) is not { } row || row < 0 || row >= GameLayout.Rulesets.MostRows)
            {
                return null;
            }

            return this.RulesetAt(this.session, row);
        }

        private static int? RowOf(string name) =>
            GameIds.Current.Ruleset(name) ?? (GameLayout.Rulesets.RowsOfThisBuild.TryGetValue(name, out var row) ? row : null);

        /// <summary>The ruleset of a row in a session's array; null when there is none or it does not point back to the session.</summary>
        private IntPtr? RulesetAt(IntPtr owner, int row) =>
            this.memory.TryRead<IntPtr>(owner + GameLayout.Rulesets.SessionRulesets + (row * IntPtr.Size), out var rules) && this.memory.IsValidAddress(rules) &&
            this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.RulesetSession, out var back) && back == owner
                ? rules
                : null;

        /// <summary>Whether the session is known and still the area's; finds it when it is not.</summary>
        private bool EnsureSession()
        {
            var state = Core.States.InGameStateObject;
            var areaNow = state.CurrentAreaInstance.Address;
            if (this.session != IntPtr.Zero && areaNow == this.area && this.PointsBackToArea(this.session, areaNow))
            {
                return true;
            }

            var now = Environment.TickCount64;
            if (areaNow == IntPtr.Zero || now - this.lastSearch < RetryMs)
            {
                return false;
            }

            this.lastSearch = now;
            (this.session, this.area) = (this.SearchSession(state.Address, areaNow), areaNow);
            if (this.session == IntPtr.Zero)
            {
                if (!this.missLogged)
                {
                    this.missLogged = true;
                    this.log.Info("Rulesets: no pointer of the area or the game state leads to a session of this area; the Breach's ring, the charges' total and the Trial's round stay unread");
                }

                return false;
            }

            this.log.Info(this.Describe());
            return true;
        }

        private bool PointsBackToArea(IntPtr candidate, IntPtr areaNow) =>
            this.memory.TryRead<IntPtr>(candidate + GameLayout.Rulesets.SessionArea, out var held) && held == areaNow;

        /// <summary>
        ///     The session: where it was found in a running game (the area's +0x5B8), where the exe stores it, else any pointer of
        ///     the area or of the game state whose own pointer to the area is the area the host already reads.
        /// </summary>
        private IntPtr SearchSession(IntPtr gameState, IntPtr areaNow)
        {
            foreach (var (root, hint, label) in new[] { (areaNow, GameLayout.Rulesets.AreaSession, "area"), (gameState, GameLayout.Rulesets.GameStateSession, "game state") })
            {
                if (root != IntPtr.Zero && this.memory.TryRead<IntPtr>(root + hint, out var named) && this.memory.IsValidAddress(named) && this.PointsBackToArea(named, areaNow))
                {
                    return named;
                }
            }

            foreach (var (root, label) in new[] { (areaNow, "area"), (gameState, "game state") })
            {
                var block = root == IntPtr.Zero ? Array.Empty<byte>() : this.memory.ReadBytes(root, GameLayout.Rulesets.GameStateSearchBytes);
                for (var offset = 0; offset + IntPtr.Size <= block.Length; offset += IntPtr.Size)
                {
                    var candidate = new IntPtr(BitConverter.ToInt64(block, offset));
                    if (candidate != IntPtr.Zero && this.memory.IsValidAddress(candidate) && this.PointsBackToArea(candidate, areaNow))
                    {
                        this.log.Info($"Rulesets: session found at {label} +0x{offset:X}, not where it was (area +0x{GameLayout.Rulesets.AreaSession:X})");
                        return candidate;
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}
