namespace Poe2Accessibility.Services
{
    using System;
    using Poe2Accessibility.Models;

    /// <summary>A price or count of the focused window that the player's own press changed (<see cref="NumberChanges"/>).</summary>
    public sealed partial class ScreenNarrator
    {
        private long numberSaidAt = long.MinValue / 2;

        private void OnNumbersChanged(ScreenChange change, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            if (!this.inGame || focusLayer == IntPtr.Zero || change.Layer.Address != focusLayer || change.Layer.Address == this.HudLayer)
            {
                return;
            }

            var now = Environment.TickCount64;
            var sinceInput = this.SinceConfirm?.Invoke() ?? long.MaxValue;
            // A field's value stepped by the d-pad and the triggers (the trade market's range: "+1 [cruceta derecha]"): any
            // press is what changed it, and each step is said over the last.
            var sincePress = this.SincePress?.Invoke() ?? long.MaxValue;
            var stepped = focus?.Role == UiRole.TextField && sincePress <= NumberChanges.RecentInputMs;
            sinceInput = stepped ? Math.Min(sinceInput, sincePress) : sinceInput;
            for (var i = 0; i < change.Texts.Count && i < change.Before.Count; i++)
            {
                var text = change.Texts[i].Text;
                if (focus != null && (text == focus.Text || text == focus.Value))
                {
                    continue; // the focused control's own value is the focus tracker's to say
                }

                if (NumberChanges.Said(change.Before[i], text, sinceInput, now - this.numberSaidAt, stepped) is { } line)
                {
                    this.numberSaidAt = now;
                    this.Speak(stepped ? "value stepped" : "number changed after a press", line, stepped);
                    return;
                }
            }
        }
    }
}
