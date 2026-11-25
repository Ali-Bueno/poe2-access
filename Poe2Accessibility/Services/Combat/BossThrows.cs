namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>A fight in which the boss must be made to throw at a part of its arena, by the game's own ids (docs/research/boss-mechanics.md, "Torvian").</summary>
    /// <param name="Boss">What the ids of the boss's forms begin with.</param>
    /// <param name="Carries">The boss's state while it holds what it throws.</param>
    /// <param name="Helper">What the ids of the helpers the throw interrupts begin with.</param>
    /// <param name="Busy">The helpers' state while they work.</param>
    /// <param name="Stopped">The value of that state, and above it, in which they are done for good.</param>
    /// <param name="Target">What the throw must hit: each helper stands by one.</param>
    public sealed record BossThrowFight(string Boss, string Carries, string Helper, string Busy, long Stopped, string Target);

    /// <summary>The target worth a throw now: where it stands, whether the boss holds something to throw, whether a throw at the player flies at it.</summary>
    public readonly record struct BossThrowAim(Vector2 At, bool Carrying, bool LinedUp);

    /// <summary>
    ///     Which of an arena's targets the boss's throw should hit: those whose helper is at work, the nearest one kept until
    ///     another is clearly nearer, and whether the player stands on the line from the boss to it. Pure.
    /// </summary>
    public sealed class BossThrows
    {
        /// <summary>
        ///     Torvian's boulder at the scaffolding by a Twilight Priest: the game's text of guard_boss_utility_buff, the files'
        ///     states `boulder` and `active` (1 begins, 2 and 3 empower, 4 is the end), and the fight of 2026-11-25.
        /// </summary>
        public static readonly BossThrowFight Torvian = new(
            "Metadata/Monsters/TwilightOrderSoldiers/TwilightOrderGuardBoss",
            "boulder",
            "Metadata/Monsters/TwilightOrderSorceror/TwilightOrderSorcererGuardBoss",
            "active",
            4,
            "Metadata/Terrain/Gallows/Act4/4_8b/Objects/BossRoomScaffolding/Scaffolding");

        /// <summary>A helper stands this near its own target (world units): Torvian's priests 35 to 88, the targets 900 apart.</summary>
        public const float OwnWorld = 250f;

        /// <summary>A target this much nearer takes the lead (world units, two steps): a fight's dance does not swap it back and forth.</summary>
        private const float NearerWorld = 200f;

        /// <summary>
        ///     How far off the boulder's line the target's centre may be (world units): twice the scaffolding's object_size of 5
        ///     cells, for the boulder's own width. The game's rule is not in the files, and no hit was measured.
        /// </summary>
        public const float LineWorld = 110f;

        private Vector2? chosen;

        public void Reset() => this.chosen = null;

        /// <summary>The target to use now; null when no helper is at work.</summary>
        public Vector2? Pick(IReadOnlyList<(Vector2 At, bool Busy)> helpers, IReadOnlyList<Vector2> targets, Vector2 player)
        {
            var busy = targets.Where(target => helpers.Any(helper => helper.Busy && Vector2.Distance(helper.At, target) <= OwnWorld))
                .OrderBy(target => Vector2.Distance(target, player)).ToList();
            if (busy.Count == 0)
            {
                this.chosen = null;
            }
            else if (this.chosen is not { } kept || !busy.Contains(kept) || Vector2.Distance(kept, player) - Vector2.Distance(busy[0], player) >= NearerWorld)
            {
                this.chosen = busy[0];
            }

            return this.chosen;
        }

        /// <summary>Whether a throw at the player flies on at the target: the player between the boss and it, near enough to the line.</summary>
        public static bool LinedUp(Vector2 boss, Vector2 player, Vector2 target)
        {
            var throwWay = player - boss;
            if (throwWay.LengthSquared() < 1f)
            {
                return false;
            }

            throwWay = Vector2.Normalize(throwWay);
            var to = target - boss;
            var along = Vector2.Dot(to, throwWay);
            return along >= Vector2.Distance(boss, player) - LineWorld && Vector2.Distance(to, throwWay * along) <= LineWorld;
        }
    }
}
