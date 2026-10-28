namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     When to sound the parry cue, a swell that stops dead on the moment to press (<see cref="CueSynth.Parry"/>), and
    ///     which blows earn it (Parry blocks melee and projectile hits; poe2db Parry, 0.5): a strike from a monster within
    ///     reach, or a projectile shot by a rare or unique — a pack of archers would ring without pause. Pure.
    /// </summary>
    public static class ParryTiming
    {
        /// <summary>Parry's attack time (poe2db, 0.5): a second parry cannot start sooner, so closer cues would ask for one.</summary>
        public const int RepeatMs = 750;

        /// <summary>Whether this blow is one to parry.</summary>
        public static bool Worth(DodgeThreat threat, bool projectile, bool rareOrUnique, float gapWorld, float reachWorld) => threat switch
        {
            DodgeThreat.Melee => gapWorld <= reachWorld,
            DodgeThreat.Ranged => projectile && rareOrUnique,
            _ => false,
        };

        /// <summary>When a cue of <paramref name="cueMs"/> starts so that it stops <paramref name="leadMs"/> before the blow.</summary>
        public static long StartAt(long blowAt, int leadMs, int cueMs) => blowAt - leadMs - cueMs;

        /// <summary>Whether a cue starting now still stops before the blow: pressing on its stop must still parry.</summary>
        public static bool InTime(long blowAt, int cueMs, long now) => now + cueMs <= blowAt;
    }
}
