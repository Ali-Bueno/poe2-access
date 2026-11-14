namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What is news of the league mechanics in an area: a followed object's state reaching a value of <see cref="LeaguePieces.Cues"/>
    ///     (each change once, per object), a pit's icon dimming, a possessed monster, a shrine's buff, the Delirium fog. Pure.
    /// </summary>
    public sealed class LeagueNews
    {
        private readonly Dictionary<uint, Dictionary<string, long>> states = new();
        private readonly Dictionary<uint, string> icons = new();
        private readonly HashSet<(uint, LeagueLine)> saidOnce = new();
        private readonly HashSet<uint> possessed = new();
        private readonly HashSet<string> shrines = new(StringComparer.Ordinal);
        private bool shrinesKnown;
        private int? fog;

        /// <summary>Gets a value indicating whether a Delirium mirror of this area has been walked through.</summary>
        public bool DeliriumRunning { get; private set; }

        /// <summary>A new area: nothing is known of its objects.</summary>
        public void Reset()
        {
            this.states.Clear();
            this.icons.Clear();
            this.saidOnce.Clear();
            this.possessed.Clear();
            this.shrines.Clear();
            (this.shrinesKnown, this.fog, this.DeliriumRunning) = (false, null, false);
        }

        /// <summary>The lines an object's states and icon make news of since it was last seen; what it shows when first seen is not news.</summary>
        public List<LeagueLine> Heard(uint id, LeaguePiece piece, IEnumerable<(string Name, long Value)> read, string icon)
        {
            var now = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var (name, value) in read)
            {
                now[name] = value;
            }

            var first = !this.states.TryGetValue(id, out var before);
            var lines = new List<LeagueLine>();
            foreach (var cue in LeaguePieces.Cues)
            {
                if (cue.Piece != piece || !now.TryGetValue(cue.State, out var value) || value != cue.Value ||
                    (cue.Unless is { } unless && now.GetValueOrDefault(unless) > 0))
                {
                    continue;
                }

                var reached = first ? cue.FirstSight
                    : before!.TryGetValue(cue.State, out var was) && was != value && (cue.From is not { } from || was == from);
                if (reached && (!cue.Once || this.saidOnce.Add((id, cue.Line))))
                {
                    lines.Add(cue.Line);
                }
            }

            if (this.icons.TryGetValue(id, out var iconBefore) && iconBefore != icon && LeaguePieces.IconLine(piece, icon) is { } dimmed)
            {
                lines.Add(dimmed);
            }

            this.states[id] = now;
            this.icons[id] = icon;
            this.DeliriumRunning |= piece == LeaguePiece.DeliriumMirror && now.GetValueOrDefault(LeaguePieces.MirrorUsedState) > 0;
            return lines;
        }

        /// <summary>A monster an Azmeri spirit possesses, said once.</summary>
        public bool Possessed(uint id) => this.possessed.Add(id);

        /// <summary>The shrines whose buffs have just come on the player; those there as the area opens are not news.</summary>
        public List<ShrineKind> Shrines(IEnumerable<string> buffKeys)
        {
            var now = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in buffKeys)
            {
                var id = BuffIds.In(key, LeaguePieces.ShrineBuffs.ContainsKey);
                if (LeaguePieces.ShrineBuffs.ContainsKey(id))
                {
                    now.Add(id);
                }
            }

            var begun = this.shrinesKnown ? now.Where(id => !this.shrines.Contains(id)).Select(id => LeaguePieces.ShrineBuffs[id]).ToList() : new List<ShrineKind>();
            this.shrines.Clear();
            this.shrines.UnionWith(now);
            this.shrinesKnown = true;
            return begun;
        }

        /// <summary>The fog stat changing side while a Delirium encounter runs here: in it, or out of it; null otherwise or unread.</summary>
        public LeagueLine? Fog(int? inFog)
        {
            if (inFog is not { } now)
            {
                return null;
            }

            var was = this.fog;
            this.fog = now;
            return was is not { } before || !this.DeliriumRunning || (before > 0) == (now > 0) ? null
                : now > 0 ? LeagueLine.InFog : LeagueLine.OutOfFog;
        }
    }

    /// <summary>What is news while Expedition charges are placed: what the charge at the cursor would blow, a refused place, a charge set. Pure.</summary>
    public sealed class PlacementNews
    {
        /// <summary>The fewest milliseconds between two readings of what the charge would blow, so moving the stick is no stream of speech.</summary>
        public const long CoverEveryMs = 700;

        private readonly HashSet<uint> said = new();
        private readonly HashSet<uint> placed = new();
        private long lastSaid = long.MinValue / 2;
        private bool placing;
        private bool blocked;
        private bool chargesKnown;

        /// <summary>A new area: no charge is placed.</summary>
        public void Reset()
        {
            this.said.Clear();
            this.placed.Clear();
            (this.lastSaid, this.placing, this.blocked, this.chargesKnown) = (long.MinValue / 2, false, false, false);
        }

        /// <summary>
        ///     What the charge being placed would blow, by kind and count (empty: nothing), when it changed since last said and that
        ///     was long enough ago; null otherwise. Placing starts from nothing covered, said only once something is.
        /// </summary>
        public IReadOnlyList<(ExpeditionFind Find, int Count)>? Covers(IReadOnlyCollection<(uint Id, ExpeditionFind Find)> covered, bool placingNow, long now)
        {
            if (!placingNow || !this.placing)
            {
                this.said.Clear();
                this.placing = placingNow;
                if (!placingNow || covered.Count == 0)
                {
                    return null;
                }
            }

            if (this.said.SetEquals(covered.Select(piece => piece.Id)) || now - this.lastSaid < CoverEveryMs)
            {
                return null;
            }

            this.said.Clear();
            this.said.UnionWith(covered.Select(piece => piece.Id));
            this.lastSaid = now;
            return covered.GroupBy(piece => piece.Find).OrderBy(group => group.Key).Select(group => (group.Key, group.Count())).ToList();
        }

        /// <summary>The charge cannot go where it stands: true as that begins.</summary>
        public bool Refused(bool blockedNow)
        {
            var began = blockedNow && !this.blocked;
            this.blocked = blockedNow;
            return began;
        }

        /// <summary>How many charges the area has once a new one appears; null otherwise, and for those there as the area opens.</summary>
        public int? Placed(IEnumerable<uint> charges)
        {
            var grew = false;
            foreach (var id in charges)
            {
                grew |= this.placed.Add(id);
            }

            var said = grew && this.chargesKnown;
            this.chargesKnown = true;
            return said ? this.placed.Count : null;
        }
    }
}
