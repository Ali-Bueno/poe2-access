namespace Poe2Accessibility.Services
{
    /// <summary>The scaffolding's cue of <see cref="CueSynth"/>: where a boss must be made to throw its boulder (Torvian), the owner's "un metal o algo" (2026-11-25).</summary>
    public static partial class CueSynth
    {
        private const float ScaffoldLevel = 0.8f;

        /// <summary>The second strike of the lined-up cue comes this long (s) after the first.</summary>
        private const float ScaffoldTwiceSeconds = 0.13f;

        /// <summary>A free bar's partials: a struck steel tube, no chord and no bell.</summary>
        private static readonly float[] TubeRatios = { 1f, 2.76f, 5.4f, 8.93f };
        private static readonly float[] TubeLevels = { 1f, 0.8f, 0.5f, 0.25f };

        /// <summary>One blow on a scaffolding tube.</summary>
        public static float[] Scaffold(int sampleRate) => Normalized(Room(Tube(sampleRate), sampleRate, 0.2f), ScaffoldLevel);

        /// <summary>Two quick blows: a throw now would hit it.</summary>
        public static float[] ScaffoldTwice(int sampleRate)
        {
            var tube = Tube(sampleRate);
            return Normalized(Room(Sum(tube, Delayed(tube, ScaffoldTwiceSeconds, sampleRate)), sampleRate, 0.2f), ScaffoldLevel);
        }

        private static float[] Tube(int sampleRate) => Modal(410f, TubeRatios, TubeLevels, 0.22f, 0.4f, sampleRate);
    }
}
