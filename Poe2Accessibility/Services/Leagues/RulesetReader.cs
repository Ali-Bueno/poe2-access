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

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly Dictionary<string, IntPtr> byName = new(StringComparer.Ordinal);
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
            this.byName.Clear();
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
                    BreachRings.ValueOf(from, to, duration, elapsed) is not { } value || state > GameLayout.Rulesets.BreachLastState || !IsGridCell(x, y))
                {
                    continue;
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

        /// <summary>Research: what was found, for the log and the dev command.</summary>
        public string Describe() =>
            this.session == IntPtr.Zero ? "Rulesets: session not found" : $"Rulesets: session 0x{this.session.ToInt64():X}, {this.byName.Count} rulesets: {string.Join(", ", this.byName.Keys.OrderBy(name => name, StringComparer.Ordinal))}";

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
        private int? VectorCount(IntPtr vector, int stride)
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

            return bytes > 0 && bytes % stride == 0 && bytes / stride <= MaxRecords && this.memory.IsValidAddress(first) ? (int)(bytes / stride) : null;
        }

        /// <summary>A ruleset by the Id of its Rulesets.dat row; null when the area has none of that name.</summary>
        private IntPtr? Find(string name)
        {
            if (!this.EnsureSession())
            {
                return null;
            }

            return this.byName.TryGetValue(name, out var rules) ? rules : null;
        }

        /// <summary>Whether the session is known and still the area's; finds it, and its rulesets, when it is not.</summary>
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
            this.byName.Clear();
            (this.session, this.area) = (this.SearchSession(state.Address, areaNow), areaNow);
            if (this.session == IntPtr.Zero)
            {
                if (!this.missLogged)
                {
                    this.missLogged = true;
                    this.log.Info("Rulesets: no pointer of the game state leads to a session of this area; the Breach's ring, the charges' total and the Trial's round stay unread");
                }

                return false;
            }

            for (var row = 0; row < GameLayout.Rulesets.MostRows; row++)
            {
                if (this.memory.TryRead<IntPtr>(this.session + GameLayout.Rulesets.SessionRulesets + (row * IntPtr.Size), out var rules) && this.memory.IsValidAddress(rules) &&
                    this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.RulesetSession, out var owner) && owner == this.session &&
                    this.memory.TryRead<IntPtr>(rules + GameLayout.Rulesets.RulesetRow, out var datRow) && this.memory.IsValidAddress(datRow) &&
                    this.memory.TryRead<IntPtr>(datRow, out var text) && this.memory.IsValidAddress(text) &&
                    this.elements.ReadUtf16BufferAt(text, MaxNameChars) is { Length: > 0 } name)
                {
                    this.byName[name] = rules;
                }
            }

            this.log.Info(this.Describe());
            return true;
        }

        private bool PointsBackToArea(IntPtr candidate, IntPtr areaNow) =>
            this.memory.TryRead<IntPtr>(candidate + GameLayout.Rulesets.SessionArea, out var held) && held == areaNow;

        /// <summary>
        ///     The session among the game state's pointers: the one the exe names first (+0x340), else any whose own pointer to the
        ///     area is the area the host already reads.
        /// </summary>
        private IntPtr SearchSession(IntPtr gameState, IntPtr areaNow)
        {
            if (gameState == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (this.memory.TryRead<IntPtr>(gameState + GameLayout.Rulesets.GameStateSession, out var named) && this.memory.IsValidAddress(named) && this.PointsBackToArea(named, areaNow))
            {
                return named;
            }

            var block = this.memory.ReadBytes(gameState, GameLayout.Rulesets.GameStateSearchBytes);
            for (var offset = 0; offset + IntPtr.Size <= block.Length; offset += IntPtr.Size)
            {
                var candidate = new IntPtr(BitConverter.ToInt64(block, offset));
                if (candidate != IntPtr.Zero && this.memory.IsValidAddress(candidate) && this.PointsBackToArea(candidate, areaNow))
                {
                    this.log.Info($"Rulesets: session found at game state +0x{offset:X}, not +0x{GameLayout.Rulesets.GameStateSession:X}");
                    return candidate;
                }
            }

            return IntPtr.Zero;
        }
    }
}
