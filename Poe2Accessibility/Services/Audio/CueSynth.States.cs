namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>Enemy-state, parry, unblockable, skill-ready and hazard cues of <see cref="CueSynth"/>, and the struck notes they are made of.</summary>
    public static partial class CueSynth
    {
        /// <summary>
        ///     Enemy states (EnemyStateWatch), one struck figure each: primed for stun a wind-up rising an octave and a half;
        ///     heavy stunned a low gong; frozen three
        ///     icy sparkles rising; electrocuted a crackling buzz; pinned two nail knocks; armour broken a gritty crack
        ///     falling; dazed a wobbling tone sinking; parried a single bright ting.
        /// </summary>
        private const float GritSeconds = 0.0025f;
        private const int GritSeed = 7;
        private static readonly float[] Bell = { 1f, 2.76f, 5.4f };
        private static readonly float[] Glass = { 1f, 1.012f, 2f };
        private static readonly float[] Buzz = { 1f, 2f, 3f, 4f, 5f, 6f, 7f };
        private static readonly float[] Wood = { 1f, 2.7f };
        private static readonly float[] Rubble = { 1f, 1.5f, 2.3f };
        private static readonly float[] Pure = { 1f };
        private static readonly float[] Organ = { 1f, 2f, 3f };
        private static readonly float[] Metal = { 1f, 2.92f };
        private static readonly float[] Plate = { 1f, 1.58f, 2.37f, 3.41f };

        /// <summary>
        ///     Parry now: a reverse hit — a struck buckler (a low plate and a ringing bell partial) in a stone room, played
        ///     backwards, so it swells in and stops dead on the moment to press Parry ("the attack is coming, parry it",
        ///     the user's ask, 2026-10-28). The dodge warning ends in a bass drop and a tail; this one ends in silence.
        /// </summary>
        private static readonly StateNote[] ParryHitNotes =
        {
            new(0f, 0.5f, 210f, 200f, Plate, 0.16f, Grit: 0.25f),
            new(0f, 0.35f, 840f, 830f, Bell, 0.09f, Grit: 0.15f),
        };

        private const float ParryRoom = 0.7f;
        private const float ParrySwellSeconds = 0.32f;
        private const float ParryFadeInSeconds = 0.03f;
        private const float ParryLevel = 0.8f;

        /// <summary>
        ///     An attack that cannot be blocked (the game's red glow): two low brass stabs a tritone apart, short and harsh, at
        ///     the wind-up, before the dodge warning itself.
        /// </summary>
        private static readonly StateNote[] UnblockableNotes =
        {
            new(0f, 0.09f, 147f, 147f, Buzz, 0.08f, Grit: 0.2f),
            new(0f, 0.09f, 208f, 208f, Buzz, 0.08f, Grit: 0.2f),
            new(0.15f, 0.09f, 147f, 147f, Buzz, 0.08f, Grit: 0.2f),
            new(0.15f, 0.09f, 208f, 208f, Buzz, 0.08f, Grit: 0.2f),
        };

        private const float UnblockableLevel = 0.7f;

        /// <summary>
        ///     A skill usable again (SkillCooldownWatch): a bolt sliding home, two equal gritty metal clicks 90 ms apart in a
        ///     small stone room, so nothing rises (chosen by the user from three candidates, 2026-10-31).
        /// </summary>
        private static readonly StateNote[] SkillReadyNotes =
        {
            new(0f, 0.05f, 1400f, 1300f, Metal, 0.012f, Grit: 0.5f),
            new(0.09f, 0.07f, 1400f, 1300f, Metal, 0.02f, Grit: 0.5f),
        };

        private const float SkillReadyRoom = 0.3f;
        private const float SkillReadyLevel = 0.3f;

        /// <summary>Twice the unblockable's harmonics: an octave lower, they keep the hazard alarm's body in the mids.</summary>
        private static readonly float[] HazardBuzz = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f };

        /// <summary>
        ///     Harmful ground under the player: two stabs of notes a semitone apart, grinding against each other, kin to the
        ///     unblockable cue's tritone but repeating, an octave under it (chosen by the user, 2026-11-04).
        /// </summary>
        private static readonly StateNote[] HazardNotes =
        {
            new(0f, 0.12f, 73.5f, 73.5f, HazardBuzz, 0.1f, Grit: 0.3f),
            new(0f, 0.12f, 78f, 78f, HazardBuzz, 0.1f, Grit: 0.3f),
            new(0.2f, 0.12f, 73.5f, 73.5f, HazardBuzz, 0.1f, Grit: 0.3f),
            new(0.2f, 0.12f, 78f, 78f, HazardBuzz, 0.1f, Grit: 0.3f),
        };

        private const float HazardLoopSeconds = 0.56f;
        private const float HazardLevel = 0.47f;

        public static float[] StateCue(EnemyState state, int sampleRate)
        {
            var (notes, level) = state switch
            {
                EnemyState.PrimedForStun => (new[] { new StateNote(0f, 0.18f, 300f, 900f, Organ, 0.3f) }, 0.3f),
                EnemyState.HeavyStunned => (new[] { new StateNote(0f, 0.35f, 196f, 185f, Bell, 0.18f) }, 0.45f),
                EnemyState.Frozen => (new[]
                {
                    new StateNote(0f, 0.09f, 2349f, 2349f, Glass, 0.05f),
                    new StateNote(0.05f, 0.09f, 2794f, 2794f, Glass, 0.05f),
                    new StateNote(0.1f, 0.12f, 3520f, 3520f, Glass, 0.06f),
                }, 0.28f),
                EnemyState.Electrocuted => (new[] { new StateNote(0f, 0.2f, 110f, 90f, Buzz, 0.12f, Grit: 0.6f) }, 0.32f),
                EnemyState.Pinned => (new[]
                {
                    new StateNote(0f, 0.06f, 880f, 700f, Wood, 0.02f, Grit: 0.3f),
                    new StateNote(0.08f, 0.06f, 880f, 700f, Wood, 0.02f, Grit: 0.3f),
                }, 0.4f),
                EnemyState.ArmourBroken => (new[] { new StateNote(0f, 0.18f, 520f, 160f, Rubble, 0.07f, Grit: 0.8f) }, 0.4f),
                EnemyState.Dazed => (new[] { new StateNote(0f, 0.32f, 700f, 500f, Pure, 0.25f, VibratoHz: 7f, VibratoDepth: 0.12f) }, 0.28f),
                _ => (new[] { new StateNote(0f, 0.14f, 1568f, 1568f, Metal, 0.05f) }, 0.3f),
            };

            return Struck(notes, level, sampleRate);
        }

        /// <summary>Parry now (<see cref="ParryHitNotes"/>): the cue's last sample is the moment to press.</summary>
        public static float[] Parry(int sampleRate) => ReverseHit(Struck(ParryHitNotes, 1f, sampleRate), ParryRoom, sampleRate);

        /// <summary>An attack that cannot be blocked (<see cref="UnblockableNotes"/>).</summary>
        public static float[] Unblockable(int sampleRate) => Struck(UnblockableNotes, UnblockableLevel, sampleRate);

        /// <summary>A skill usable again (<see cref="SkillReadyNotes"/>).</summary>
        public static float[] SkillReady(int sampleRate) => Normalized(Room(Struck(SkillReadyNotes, 1f, sampleRate), sampleRate, SkillReadyRoom), SkillReadyLevel);

        /// <summary>The hazard alarm's loop (<see cref="HazardNotes"/>): its stabs, then a rest up to the seam.</summary>
        public static float[] HazardAlarm(int sampleRate)
        {
            var stabs = Struck(HazardNotes, HazardLevel, sampleRate);
            var loop = new float[(int)(HazardLoopSeconds * sampleRate)];
            Array.Copy(stabs, loop, stabs.Length);
            return loop;
        }

        /// <summary>A hit and its room played backwards, the last <see cref="ParrySwellSeconds"/> of it: a swell into a dead stop.</summary>
        internal static float[] ReverseHit(float[] hit, float room, int sampleRate)
        {
            var reversed = Room(hit, sampleRate, room);
            Array.Reverse(reversed);
            var length = Math.Min(reversed.Length, (int)(ParrySwellSeconds * sampleRate));
            var skip = reversed.Length - length;
            var fade = (int)(ParryFadeInSeconds * sampleRate);
            var swell = new float[length];
            for (var i = 0; i < length; i++)
            {
                swell[i] = reversed[skip + i] * MathF.Min(1f, (float)i / fade);
            }

            return Normalized(swell, ParryLevel);
        }

        /// <summary>Struck notes summed and brought to <paramref name="level"/> (enemy states, parry).</summary>
        internal static float[] Struck(StateNote[] notes, float level, int sampleRate)
        {
            var samples = new float[(int)(notes.Max(note => note.Start + note.Seconds) * sampleRate) + 1];
            var random = new Random(GritSeed);
            var gritStep = Math.Max(1, (int)(GritSeconds * sampleRate));
            foreach (var note in notes)
            {
                var start = (int)(note.Start * sampleRate);
                var phases = new double[note.Ratios.Length];
                var grit = 1f;
                for (var i = 0; i < (int)(note.Seconds * sampleRate); i++)
                {
                    var t = (float)i / sampleRate;
                    var hz = (note.FromHz + ((note.ToHz - note.FromHz) * t / note.Seconds)) *
                             (1f + (note.VibratoDepth * MathF.Sin(2 * MathF.PI * note.VibratoHz * t)));
                    if (i % gritStep == 0)
                    {
                        grit = 1f - (note.Grit * (float)random.NextDouble());
                    }

                    var value = 0f;
                    for (var p = 0; p < note.Ratios.Length; p++)
                    {
                        phases[p] += hz * note.Ratios[p] / sampleRate;
                        value += MathF.Sin((float)(2 * Math.PI * phases[p])) / (p + 1);
                    }

                    samples[start + i] += value * grit * MathF.Exp(-t / note.Decay) * MathF.Min(1f, t / AttackSeconds) *
                                          MathF.Min(1f, (note.Seconds - t) / TapReleaseSeconds);
                }
            }

            return Normalized(samples, level);
        }

        /// <summary>One struck note of an enemy-state cue: onset, length, pitch glide, partials (falling 1/n), decay, wobble and crackle.</summary>
        internal readonly record struct StateNote(
            float Start, float Seconds, float FromHz, float ToHz, float[] Ratios, float Decay, float VibratoHz = 0f, float VibratoDepth = 0f, float Grit = 0f);
    }
}
