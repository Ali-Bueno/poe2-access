namespace Poe2Accessibility.Tests
{
    using System;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Hand-made screen snapshots the screen-change tests share.</summary>
    internal static class ScreenFixtures
    {
        public static ScreenDiffer Settled(out ScreenSnapshot screen)
        {
            var differ = new ScreenDiffer();
            screen = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(11, "All I need")));
            differ.Apply(screen);
            differ.Apply(screen);
            return differ;
        }

        public static ScreenSnapshot Snapshot(params ScreenLayer[] layers) => new((IntPtr)100, layers);

        public static ScreenLayer Layer(long address, int index, bool controls, params ScreenText[] texts) =>
            new((IntPtr)address, index, texts, controls, false);

        public static ScreenText Text(long address, string text) => new((IntPtr)address, text, string.Empty, false, true);
    }
}
