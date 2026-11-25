namespace Poe2Accessibility.Services
{
    /// <summary>The scaffolding's cue of <see cref="CueSynth"/>: where a boss must be made to throw its boulder (Torvian), the owner's pick of four (2026-11-25, audition page https://claude.ai/artifact/N8HuKVHJJLyChG85nXgYbs): a struck sheet of metal.</summary>
    public static partial class CueSynth
    {
        private const float ScaffoldLevel = 0.8f;

        /// <summary>The second strike of the lined-up cue comes this long (s) after the first.</summary>
        private const float ScaffoldTwiceSeconds = 0.13f;

        /// <summary>A plate's partials, close together and in no harmony: rough, no chord and no bell.</summary>
        private static readonly float[] SheetRatios = { 1f, 1.47f, 2.09f, 2.56f, 3.1f, 4.3f };
        private static readonly float[] SheetLevels = { 1f, 0.9f, 0.8f, 0.6f, 0.5f, 0.3f };

        /// <summary>One blow on a sheet of metal.</summary>
        public static float[] Scaffold(int sampleRate) => Normalized(Room(Sheet(sampleRate), sampleRate, 0.2f), ScaffoldLevel);

        /// <summary>Two quick blows: a throw now would hit it.</summary>
        public static float[] ScaffoldTwice(int sampleRate)
        {
            var sheet = Sheet(sampleRate);
            return Normalized(Room(Sum(sheet, Delayed(sheet, ScaffoldTwiceSeconds, sampleRate)), sampleRate, 0.2f), ScaffoldLevel);
        }

        private static float[] Sheet(int sampleRate) => Modal(290f, SheetRatios, SheetLevels, 0.2f, 0.4f, sampleRate);
    }
}
