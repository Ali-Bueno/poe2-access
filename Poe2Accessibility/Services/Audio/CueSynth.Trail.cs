namespace Poe2Accessibility.Services
{
    /// <summary>The trail cue of <see cref="CueSynth"/>: the way a river, a road or a swarm goes on (docs/research/map-hints.md, "Trails").</summary>
    public static partial class CueSynth
    {
        private const float TrailLevel = 0.45f;

        /// <summary>Two dull knocks on wood, as steps on ahead.</summary>
        public static float[] Trail(int sampleRate) => TrailKnocks(sampleRate, 210f, 0.13f, 1400f);

        /// <summary>Two struck bodies, the second softer.</summary>
        internal static float[] TrailKnocks(int sampleRate, float hz, float gapSeconds, float cutoffHz)
        {
            var knock = Damped(Modal(hz, new[] { 1f, 2.7f, 4.1f }, new[] { 1f, 0.4f, 0.2f }, 0.05f, 0.16f, sampleRate), cutoffHz, sampleRate);
            var gap = (int)(gapSeconds * sampleRate);
            var both = new float[gap + knock.Length];
            for (var i = 0; i < knock.Length; i++)
            {
                both[i] += knock[i];
                both[gap + i] += 0.6f * knock[i];
            }

            return Normalized(Room(both, sampleRate, 0.25f), TrailLevel);
        }
    }
}
