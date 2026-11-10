namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>A living monster near the player, as the weak-point guide sees it.</summary>
    public readonly record struct BossPart(uint Id, string Name, string Path, Vector2 Position, float Radius, bool Unique, bool InPlay);

    /// <summary>
    ///     A boss made of a head and a tail of one name, where only the head is worth striking (the Devourer, Anundr;
    ///     MonsterVarieties MudBurrowerTailBoss and DevourerBossDuoTail carry no boss bar and share the head's name):
    ///     which part the guide leads to and how often it beats. Pure.
    /// </summary>
    public static class WeakPoints
    {
        /// <summary>The tail's metadata name carries this word (MudBurrowerTailBoss, DevourerBossDuoTail); the head's does not.</summary>
        public const string TailWord = "Tail";

        /// <summary>Within reach of the head the beat slows to this: a steady "you are there".</summary>
        public const int AtPartIntervalMs = 900;

        public static bool IsTail(string path) => path.AsSpan(path.LastIndexOf('/') + 1).Contains(TailWord, StringComparison.Ordinal);

        /// <summary>
        ///     The part to strike: the nearest unique monster in play named as a tail near and no tail itself; never a trap of
        ///     its name (MudBurrowerHeadTrap, not unique), so a buried head means silence.
        /// </summary>
        public static BossPart? Pick(IReadOnlyList<BossPart> monsters, Vector2 player)
        {
            var tails = monsters.Where(monster => monster.Name.Length > 0 && IsTail(monster.Path)).Select(monster => monster.Name).ToHashSet();
            return monsters.Where(monster => monster.Unique && monster.InPlay && !IsTail(monster.Path) && tails.Contains(monster.Name))
                .OrderBy(monster => Vector2.Distance(monster.Position, player))
                .Select(monster => (BossPart?)monster)
                .FirstOrDefault();
        }

        /// <summary>Time between beats for the gap between the bodies: faster as it closes, slow and steady within reach.</summary>
        public static int IntervalMs(float gap, float reach) => gap <= reach ? AtPartIntervalMs : BeaconMath.IntervalMs(gap);
    }
}
