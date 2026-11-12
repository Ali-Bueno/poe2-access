namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What the layout checks found after the game started (<see cref="LayoutProbe"/>, <see cref="InGameFields"/>):
    ///     each part logged once as found where measured, moved, or missing; the missing ones are also said once, together,
    ///     so a game update that breaks a reading is heard instead of guessed from silence (docs/compatibility.md).
    /// </summary>
    public sealed class CompatibilityReport
    {
        /// <summary>Missing parts found close together are said in one line.</summary>
        private const int GatherMs = 3000;

        private readonly PluginLog log;
        private readonly Dictionary<string, string> outcomes = new(StringComparer.Ordinal);
        private readonly List<string> unsaid = new();
        private long firstUnsaidAt;

        public CompatibilityReport(PluginLog log)
        {
            this.log = log;
            Current = this;
        }

        /// <summary>Gets the plugin's report, for the static readers that judge a layout of their own (<see cref="DatKey"/>).</summary>
        public static CompatibilityReport? Current { get; private set; }

        /// <summary>Gets how many times everything was to be checked again (a new game process); what was judged before is stale.</summary>
        public int Generation { get; private set; }

        /// <summary>The part holds what it must where it was measured.</summary>
        public void Found(string part) => this.Note(part, "ok", $"Layout: {part} where measured");

        /// <summary>The part was found elsewhere (a game update moved it): readings go on from there.</summary>
        public void Moved(string part, int from, int to) =>
            this.Note(part, $"moved {to:X}", $"Layout: {part} moved from +0x{from:X} to +0x{to:X}");

        /// <summary>The part could not be found; the readings that need it may fail.</summary>
        public void Missing(string part)
        {
            if (this.Note(part, "missing", $"Layout: {part} NOT FOUND (game update?); readings that need it may fail"))
            {
                if (this.unsaid.Count == 0)
                {
                    this.firstUnsaidAt = Environment.TickCount64;
                }

                this.unsaid.Add(part);
            }
        }

        /// <summary>Whether the part is recorded as not found now.</summary>
        public bool IsMissing(string part) => this.outcomes.TryGetValue(part, out var outcome) && outcome == "missing";

        /// <summary>Gets whether any part is recorded as not found now.</summary>
        public bool AnyMissing => this.outcomes.ContainsValue("missing");

        /// <summary>The game process changed: everything is checked again.</summary>
        public void Reset()
        {
            this.outcomes.Clear();
            this.unsaid.Clear();
            this.Generation++;
        }

        /// <summary>The missing parts not said yet, once they have gathered; empty otherwise.</summary>
        public IReadOnlyList<string> TakeUnsaid()
        {
            if (this.unsaid.Count == 0 || Environment.TickCount64 - this.firstUnsaidAt < GatherMs)
            {
                return Array.Empty<string>();
            }

            var parts = this.unsaid.Distinct().ToList();
            this.unsaid.Clear();
            return parts;
        }

        /// <summary>Records an outcome; true when it is new for that part (each change is logged once).</summary>
        private bool Note(string part, string outcome, string message)
        {
            if (this.outcomes.TryGetValue(part, out var known) && known == outcome)
            {
                return false;
            }

            this.outcomes[part] = outcome;
            if (outcome == "missing")
            {
                this.log.Warn(message);
            }
            else
            {
                this.unsaid.Remove(part); // found after all, before it was said missing
                this.log.Info(message);
            }

            return true;
        }
    }
}
