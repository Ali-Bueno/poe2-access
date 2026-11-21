namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The long blast's pure rules (<see cref="BossMechanicsWatch"/>): when an attack is one, and how far the player is from its edge.</summary>
    public static class BlastGuide
    {
        /// <summary>
        ///     A blow at least this far ahead (ms) is a long blast: time to walk out of a wide circle. The boss attacks the files
        ///     time sooner (Doryani's slowest, 3.46 s; his Triumph's slam, 3.0 s) keep the counterplay word and the dodge cue alone.
        /// </summary>
        public const int LongBlastMs = 4000;

        /// <summary>One spoken step (world units), as the beacon and the flood watch count them.</summary>
        public const float StepWorldUnits = 100f;

        /// <summary>Whether a unique's attack is a long blast: its skill sets off a circle and its blow is at least <see cref="LongBlastMs"/> away.</summary>
        public static bool Starts(bool unique, float? radius, int? blowInMs) => unique && radius is > 0f && blowInMs is >= LongBlastMs;

        /// <summary>
        ///     An attack's area a long blast can be guided out of: one shape aimed at its target, not scattered markers (a trigger
        ///     of several hits says "Move!" already) nor a mortar's guess; null otherwise.
        /// </summary>
        public static AttackArea? Guided(AttackArea? area) =>
            area is { Count: <= 1 } one && one.Shape != AttackAreas.Mortar && one.Length > 0f ? one : null;

        /// <summary>The steps (at least 1) from the player to the edge of a blast of <paramref name="radius"/> around the boss, <paramref name="gap"/> away; 0 when out of its reach.</summary>
        public static int StepsOut(float radius, float gap, float playerRadius)
        {
            var reach = radius + playerRadius + AttackAim.MarginWorld;
            return gap > reach ? 0 : Math.Max(1, (int)MathF.Ceiling((reach - gap) / StepWorldUnits));
        }
    }
}
