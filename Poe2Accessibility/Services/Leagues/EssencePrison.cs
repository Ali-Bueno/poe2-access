namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     The monsters an Essence's crystal holds. Alive, hostile and targetable (the game aims A at the monster to free it), they
    ///     rang as enemies, and the player "stayed attacking like a fool, thinking they could be hit" (user 2026-11-14): a held
    ///     monster is out of the fight until its crystal shatters, and is told so everywhere the mod asks
    ///     <see cref="EntityCatalog.IsHidden"/>. The rule is the one that already named the crystal by its monster: the two stand
    ///     in the same place (dump 2026-11-10, Mansión de Ogham).
    /// </summary>
    public static class EssencePrison
    {
        /// <summary>How near a crystal its monster stands, in world units: the same cell.</summary>
        public const float ReachWorld = 30f;

        private const string Objects = "Metadata/MiscellaneousObjects/";
        private const char InstanceMark = '@';

        /// <summary>
        ///     The crystals' files: an essence's and a map's great one (Monolith.ot, SuperMonolith.ot). The small crystals around one
        ///     (MiniMonolith.ot) have no target and no icon to check their transition against and were never measured: a monster of
        ///     theirs rings as before, rather than risk one silent after it is freed.
        /// </summary>
        private static readonly string[] Crystals = { Objects + "Monolith", Objects + "SuperMonolith" };

        /// <summary>The hidden buff the game gives a monster as its crystal frees it (BuffDefinitions 694): never held again.</summary>
        public const string FreedBuff = "monolith_monster_freedom";

        private static volatile HashSet<uint> held = new();

        /// <summary>The crystals' files, for the data step's check of the names the rules match.</summary>
        public static IReadOnlyList<string> Files => Crystals;

        /// <summary>Gets a value indicating whether any monster is held now.</summary>
        public static bool Any => held.Count > 0;

        /// <summary>Whether a crystal not yet shattered holds this monster, as last looked.</summary>
        public static bool Holds(uint id) => held.Contains(id);

        /// <summary>The monsters held now; the set is not touched again once given.</summary>
        public static void Publish(HashSet<uint> now) => held = now;

        /// <summary>Whether a file is an Essence crystal's, whatever its instance tail.</summary>
        public static bool IsCrystal(string path)
        {
            if (!path.StartsWith(Objects, StringComparison.Ordinal))
            {
                return false;
            }

            var mark = path.IndexOf(InstanceMark);
            var file = mark < 0 ? path : path[..mark];
            return Array.IndexOf(Crystals, file) >= 0;
        }

        /// <summary>
        ///     Whether a crystal still holds: its transition below the last state (the shattered one) and the game still letting it
        ///     be used, which it does only while whole; the transition unread, by that alone. Either saying no frees the monster:
        ///     better a foe heard that cannot be hit than one unheard that can.
        /// </summary>
        public static bool IsWhole((int State, int Count)? transition, bool? inPlay) =>
            transition is { } read ? read.State < read.Count && inPlay != false : inPlay == true;

        /// <summary>Whether a crystal's transition says it has shattered; false when it does not read.</summary>
        public static bool IsShattered((int State, int Count)? transition) => transition is { } read && read.State >= read.Count;

        /// <summary>The monsters standing in a crystal that still holds. Pure.</summary>
        public static HashSet<uint> Held(IReadOnlyList<Vector2> wholeCrystals, IEnumerable<(uint Id, Vector2 Place)> monsters)
        {
            var found = new HashSet<uint>();
            foreach (var (id, place) in monsters)
            {
                foreach (var crystal in wholeCrystals)
                {
                    if (Vector2.Distance(place, crystal) < ReachWorld)
                    {
                        found.Add(id);
                        break;
                    }
                }
            }

            return found;
        }
    }
}
