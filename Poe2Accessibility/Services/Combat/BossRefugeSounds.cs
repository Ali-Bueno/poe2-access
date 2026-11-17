namespace Poe2Accessibility.Services
{
    /// <summary>The sounds of a boss fight's shelter: the lead is the mod's chord (<see cref="CueSynth.Refuge"/>), the end a game sound (tools/Poe2UiExtract/SoundExtractor.cs).</summary>
    public static class BossRefugeSounds
    {
        /// <summary>The pulse that leads to the shelter (Asala's barrier), in place of the heartbeat.</summary>
        public const string Lead = "boss_refuge";

        /// <summary>Played once as the shelter ends: the player can move freely again.</summary>
        public const string Over = "boss_refuge_over";
    }
}
