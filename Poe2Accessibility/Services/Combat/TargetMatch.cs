namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Which entity the HUD's target display stands for, when only the name it shows is known (controller play). Pure.
    /// </summary>
    public static class TargetMatch
    {
        /// <summary>
        ///     The nearest entity that carries <paramref name="name"/>, and whether it is the only one: in a pack of the
        ///     same name the nearest need not be the one targeted, so its life, rarity and modifiers are not the target's.
        /// </summary>
        public static (IntPtr Address, bool Alone) Pick(string name, IEnumerable<(IntPtr Address, string Name, float Distance)> entities)
        {
            var (best, bestDistance, carriers) = (IntPtr.Zero, float.MaxValue, 0);
            foreach (var (address, entityName, distance) in entities)
            {
                if (entityName != name)
                {
                    continue;
                }

                carriers++;
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (address, distance);
                }
            }

            return (best, carriers == 1);
        }

        /// <summary>
        ///     The same pick among the carriers in play when there is one: the game targets what can be targeted, so a
        ///     boss's next form waiting untargetable under its name, or the character it was before the fight, does not
        ///     share the name with it (counted, it kept the boss's life from being said). With none in play, those set
        ///     aside are all there is to go by.
        /// </summary>
        public static (IntPtr Address, bool Alone) Pick(string name, IReadOnlyCollection<(IntPtr Address, string Name, float Distance)> inPlay,
                                                         IEnumerable<(IntPtr Address, string Name, float Distance)> setAside) =>
            inPlay.Count > 0 ? Pick(name, inPlay) : Pick(name, setAside);
    }

    /// <summary>
    ///     Entity names read once each. An entity is its address and its id together: the game hands a dead monster's
    ///     address to a new one, with a new id. A name that read empty is asked again. Pure.
    /// </summary>
    public sealed class EntityNameMemo
    {
        private readonly Dictionary<(IntPtr Address, uint Id), string> names = new();

        public string Of(IntPtr address, uint id, Func<string?> read)
        {
            if (this.names.TryGetValue((address, id), out var name))
            {
                return name;
            }

            name = read() ?? string.Empty;
            if (name.Length > 0)
            {
                this.names[(address, id)] = name;
            }

            return name;
        }

        public void Clear() => this.names.Clear();
    }

    /// <summary>
    ///     When the target under the aim is named: once it stayed there <see cref="SettleMs"/>, not again while the aim keeps
    ///     coming back to it, a sweep across a pack cutting the last name, and a pack's next nearest of the same name not
    ///     named again when that name is all that could be said. Pure.
    /// </summary>
    public sealed class TargetNaming
    {
        /// <summary>A target must stay under the aim this long before it is named (the aim sweeps over others).</summary>
        public const int SettleMs = 150;

        /// <summary>The same target is named again only after this long without it.</summary>
        public const int RepeatMs = 3000;

        /// <summary>Names within this long of the previous one cut it off (sweeping); later ones queue.</summary>
        public const int SweepMs = 2000;

        private IntPtr candidate;
        private long candidateSince;
        private IntPtr spoken;
        private string spokenText = string.Empty;
        private long spokenAt = long.MinValue / 2;

        /// <summary>A poll's target: true when it is another than the last poll's, which starts its settling.</summary>
        public bool Moved(IntPtr hovered, long now)
        {
            if (hovered == this.candidate)
            {
                return false;
            }

            (this.candidate, this.candidateSince) = (hovered, now);
            return true;
        }

        /// <summary>Whether the settled target may be named now; staying on the one named keeps it from being named again.</summary>
        public bool MayName(IntPtr hovered, bool names, bool aiming, long now)
        {
            if (names && aiming && hovered != IntPtr.Zero && now - this.candidateSince >= SettleMs && (hovered != this.spoken || now - this.spokenAt >= RepeatMs))
            {
                return true;
            }

            if (hovered == this.spoken)
            {
                this.spokenAt = now; // still on it: keep it from being named again when the aim comes back
            }

            return false;
        }

        /// <summary>The line for the target: null when it is not said (another of a pack, the same name); else whether it cuts the last name.</summary>
        public bool? Say(IntPtr hovered, string line, bool nameShared, long now)
        {
            if (nameShared && line == this.spokenText && now - this.spokenAt < RepeatMs)
            {
                (this.spoken, this.spokenAt) = (hovered, now);
                return null;
            }

            var cut = now - this.spokenAt < SweepMs;
            (this.spoken, this.spokenText, this.spokenAt) = (hovered, line, now);
            return cut;
        }
    }
}
