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
}
