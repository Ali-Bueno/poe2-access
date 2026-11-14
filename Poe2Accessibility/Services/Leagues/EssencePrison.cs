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

        /// <summary>The crystals' files: an essence's, the small ones around it, a map's great one (Monolith.ot, MiniMonolith.ot, SuperMonolith.ot).</summary>
        private static readonly string[] Crystals = { Objects + "Monolith", Objects + "MiniMonolith", Objects + "SuperMonolith" };

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
        ///     Whether a crystal still holds: by its transition when that reads (the last state is the shattered one), else by the
        ///     game still letting it be used; a small crystal has no target of its own, so one whose state does not read holds
        ///     nothing. Better a foe heard that cannot be hit than one unheard that can.
        /// </summary>
        public static bool IsWhole((int State, int Count)? transition, bool? inPlay) =>
            transition is { } read ? read.State < read.Count : inPlay == true;

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
