namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     One reading of a monster's animation controller (the AnimationController component of its animated object):
    ///     the clip playing, how far into it (seconds), its speed, the global speed and its off flag, the clip's bounds.
    /// </summary>
    public readonly record struct AnimationReading(int Clip, float Position, float Speed, float GlobalSpeed, bool GlobalOff, float Start, float End);

    /// <summary>An event of a clip, as the animation data names and times it (seconds into the clip).</summary>
    public readonly record struct ClipEvent(string Name, float Time);

    /// <summary>
    ///     How far a monster's animation really is from its blow, the way the game itself counts it (GEAL
    ///     GetTimeToNextEvent): seconds of clip ahead divided by the playing rate, so a hasted or slowed monster is timed as
    ///     it plays. Pure (docs/research/dodge-timing.md).
    /// </summary>
    public static class AttackClock
    {
        /// <summary>The blow of an attack clip; the files' first "contact" is what <c>hit-times.txt</c> times.</summary>
        public const string Contact = "contact";

        /// <summary>The game treats a rate at or below this as 1 (GetTimeToNextEvent).</summary>
        private const float GameMinRate = 0.001f;

        /// <summary>A clip played faster than this many times its speed is not a reading but a lost field.</summary>
        private const float MaxRate = 20f;

        /// <summary>A position may run this far past the clip's end before the next frame wraps it (seconds).</summary>
        private const float EndSlack = 0.1f;

        /// <summary>A clip longer than this (seconds) is no attack clip but a misread one.</summary>
        private const float MaxClipSeconds = 120f;

        private const float MsPerSecond = 1000f;

        /// <summary>FNV prime of the game's name hash (event and action names, exe 0x1D48E10 and 0x204B8A0).</summary>
        private const uint HashPrime = 0x1000193;

        private const ushort NamedEventBit = 0x8000;

        /// <summary>How many clip seconds pass per real second: the global speed (1 when its flag says so) times the clip's.</summary>
        public static float Rate(AnimationReading reading)
        {
            var global = reading.GlobalSpeed != 0f && reading.GlobalOff ? 1f : reading.GlobalSpeed;
            var rate = global * reading.Speed;
            return rate <= GameMinRate ? 1f : rate;
        }

        /// <summary>Whether a reading can be a playing clip: a clip index, finite values, the position inside the clip, a sane rate.</summary>
        public static bool IsPlausible(AnimationReading reading) =>
            reading.Clip >= 0 && float.IsFinite(reading.Position) && float.IsFinite(reading.Speed) && float.IsFinite(reading.GlobalSpeed) &&
            float.IsFinite(reading.Start) && float.IsFinite(reading.End) && reading.Start >= 0f && reading.End > reading.Start &&
            reading.End - reading.Start <= MaxClipSeconds && reading.Position >= reading.Start - EndSlack &&
            reading.Position <= reading.End + EndSlack && reading.Speed >= 0f && Rate(reading) <= MaxRate;

        /// <summary>
        ///     A contact this early in its clip (seconds) marks the cast, not a blow: the hag's DelayedBlast plays spellcast_01 with a
        ///     contact at 0.0 and its blast lands 1.45 s later (Clearfell, 2026-11-08), so timing it live said "blow passed" at every
        ///     cast; the player's aim_crossbow holds its contacts at 0.0 too. Such an attack keeps the files' or learnt time.
        /// </summary>
        public const float CastMarkSeconds = 0.05f;

        /// <summary>A clip event that times a blow: a contact past the cast mark.</summary>
        public static bool IsBlow(ClipEvent clipEvent) => clipEvent.Name == Contact && clipEvent.Time > CastMarkSeconds;

        /// <summary>
        ///     The clip time of the next blow still ahead of <paramref name="position"/> and after <paramref name="covered"/>: a
        ///     contact too close behind the last warning for a sound of its own (one per monster each 600 ms) is covered by that
        ///     warning, and the one after it is timed in its place. Beira's ice_spear_combo_01 strikes at 0.50, 0.95, 1.40, 1.70 and
        ///     2.17 s; with the covered contact marked warned, the third and fifth were warned only as the second and fourth landed,
        ///     450 ms ahead instead of 500 (Clearfell, 2026-11-08). Null when none is ahead.
        /// </summary>
        public static float? NextBlow(float position, IReadOnlyList<ClipEvent> events, float? covered)
        {
            float? next = null;
            foreach (var clipEvent in events)
            {
                if (IsBlow(clipEvent) && clipEvent.Time >= position && (covered == null || clipEvent.Time > covered) &&
                    (next == null || clipEvent.Time < next))
                {
                    next = clipEvent.Time;
                }
            }

            return next;
        }

        /// <summary>Real milliseconds until a moment of the clip (seconds into it); 0 once it is behind.</summary>
        public static int MsUntil(AnimationReading reading, float clipSeconds) =>
            (int)Math.Round(Math.Max(0f, clipSeconds - reading.Position) / Rate(reading) * MsPerSecond);

        /// <summary>Real milliseconds until the first event of that name still ahead in the clip; null when none is ahead.</summary>
        public static int? MsToNext(AnimationReading reading, IReadOnlyList<ClipEvent> events, string name)
        {
            float? next = null;
            foreach (var clipEvent in events)
            {
                if (clipEvent.Name == name && clipEvent.Time >= reading.Position && (next == null || clipEvent.Time < next))
                {
                    next = clipEvent.Time;
                }
            }

            return next is { } time ? MsUntil(reading, time) : null;
        }

        /// <summary>
        ///     Real milliseconds until a blow the files time at <paramref name="nominalMs"/> from the clip's start at speed 1
        ///     (<c>hit-times.txt</c>), from where the clip is now; null once it is behind.
        /// </summary>
        public static int? MsToNominal(AnimationReading reading, int nominalMs)
        {
            var at = reading.Start + (nominalMs / MsPerSecond);
            return at >= reading.Position ? MsUntil(reading, at) : null;
        }

        /// <summary>A new swing began: another clip, or the same one started over (its position went back).</summary>
        public static bool NewSwing(AnimationReading before, AnimationReading now) =>
            now.Clip != before.Clip || now.Position < before.Position;

        /// <summary>
        ///     Whether a swing has left the attack for the files' time: another clip than the one it started with, played while no
        ///     action is under way (a walk or idle under the last animation id). The same clip started over or come back to is the
        ///     same blow again, and another clip of the action still under way is one of its alternate swings (attack_01/_02),
        ///     which the files time at the earliest of the action's animations, so a warning there comes early, never late.
        ///     Back-to-back swings went unwarned while any restart or alternate clip stopped the files' time for the rest of the
        ///     attack (hags, grubs, golems and skeletons, Clearfell and Mud Burrow, 2026-11-08: 40-55% of their swings).
        /// </summary>
        public static bool LeftStartClip(int? startClip, AnimationReading now, bool acting) => !acting && now.Clip != startClip;

        /// <summary>The id the game gives an animation event name (the part before '#'), as kept beside each event.</summary>
        public static ushort EventId(string name) => (ushort)(Fold(Hash(name, '#')) | NamedEventBit);

        /// <summary>The id the game gives an action type name (ActionTypes.dat Id), as kept in an action and its dat row.</summary>
        public static ushort ActionId(string name) => Fold(Hash(name, null));

        private static uint Hash(string name, char? stop)
        {
            uint hash = 0;
            foreach (var character in name)
            {
                if (character == stop)
                {
                    break;
                }

                hash = unchecked(hash * HashPrime) ^ (byte)character;
            }

            return hash;
        }

        private static ushort Fold(uint hash) => (ushort)((hash >> 16) ^ hash);
    }
}
