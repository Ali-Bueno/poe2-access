namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The Trial of Chaos' Petrification Statues as a blind player needs them (docs/research/trial-of-chaos.md, "Petrification
    ///     Statues"): when one opens its eyes near, when one is looking at the player and how far the stone has built, and the line
    ///     a statue looks along once it has been met. Pure.
    /// </summary>
    public static class PetrifyingStatues
    {
        /// <summary>The eyes opening: the statue's action ChangeToStance2 (Actor animation 85), a one-poll blip before Idle.</summary>
        public const int OpenAnimation = 85;

        /// <summary>The eyes shutting: ChangeToStance1 (84).</summary>
        public const int ShutAnimation = 84;

        /// <summary>The gaze's reach: the beam UltimatumPetrificationBeam is 600 world units long (MiscBeams row 663).</summary>
        public const float ReachWorld = 600f;

        /// <summary>Eyes opening within this are sounded: the reach and a margin to step aside.</summary>
        public const float OpenHeardWorld = 800f;

        /// <summary>Half the line's width when a learned line is told; the beam's width is not in the files, a first guess.</summary>
        public const float LineHalfWidthWorld = 80f;

        /// <summary>The buff the statue puts on the player while it looks, its source the statue (live 2026-11-19: petrification_stacker_79).</summary>
        public const string GazeBuffPrefix = "petrification_stacker";

        /// <summary>The petrification built up, a charge each, 3.5 s each and gone after the gaze (petrification_stack_79).</summary>
        public const string StackBuffPrefix = "petrification_stack";

        /// <summary>Turned to stone (BuffDefinitions <c>petrified</c>, stat is_petrified).</summary>
        public const string StoneBuffPrefix = "petrified";

        /// <summary>The gaze's pulse when it meets the player, and at its fastest.</summary>
        public const int SlowestPulseMs = 650;
        public const int FastestPulseMs = 200;
        private const int PulseStepMs = 90;

        /// <summary>The statue's eyes after this animation: open on 85, shut on 84, else as they were (Idle says neither).</summary>
        public static bool Eyes(int animation, bool wereOpen) =>
            animation == OpenAnimation || (animation != ShutAnimation && wereOpen);

        /// <summary>The time between two gaze pulses at this much stone built up: quicker as it grows.</summary>
        public static int PulseMs(int stacks) => Math.Max(FastestPulseMs, SlowestPulseMs - (Math.Max(0, stacks) * PulseStepMs));

        /// <summary>
        ///     Whether the player stands in a statue's line: within its reach in front of it and near the line it looks along
        ///     (<paramref name="facing"/>, a unit vector learned from where the player stood when it looked).
        /// </summary>
        public static bool InLine(Vector2 statue, Vector2 facing, Vector2 player)
        {
            var offset = player - statue;
            var along = Vector2.Dot(offset, facing);
            if (along <= 0f || along > ReachWorld)
            {
                return false;
            }

            var across = MathF.Abs((offset.X * facing.Y) - (offset.Y * facing.X));
            return across <= LineHalfWidthWorld;
        }

        /// <summary>The way a statue looks, from it to the player it gazed at; null when they stand on one spot.</summary>
        public static Vector2? Facing(Vector2 statue, Vector2 player) =>
            Vector2.Distance(statue, player) is > 1f and var length ? (player - statue) / length : null;

        /// <summary>The statue looking at the player and the stone built up, from the player's buffs (id, source, charges).</summary>
        public static (uint Statue, int Stacks)? Gaze(IEnumerable<(string Id, uint Source, int Charges)> buffs)
        {
            var list = buffs.ToList();
            var gaze = list.FirstOrDefault(buff => buff.Id.StartsWith(GazeBuffPrefix, StringComparison.Ordinal));
            if (gaze.Id == null)
            {
                return null;
            }

            var stack = list.FirstOrDefault(buff => buff.Id.StartsWith(StackBuffPrefix, StringComparison.Ordinal) && !buff.Id.StartsWith(GazeBuffPrefix, StringComparison.Ordinal));
            return (gaze.Source, stack.Id == null ? 0 : Math.Max(1, stack.Charges));
        }

        /// <summary>Whether the player has been turned to stone.</summary>
        public static bool IsStone(IEnumerable<string> buffIds) =>
            buffIds.Any(id => id.StartsWith(StoneBuffPrefix, StringComparison.Ordinal) && !id.StartsWith("petrified_blood", StringComparison.Ordinal));
    }
}
