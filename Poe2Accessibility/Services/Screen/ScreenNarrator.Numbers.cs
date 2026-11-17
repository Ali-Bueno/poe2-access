namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>A price or count of the focused window that the player's own press changed (<see cref="NumberChanges"/>).</summary>
    public sealed partial class ScreenNarrator
    {
        private long numberSaidAt = long.MinValue / 2;

        private void OnNumbersChanged(ScreenChange change, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            // A field's value stepped by the d-pad and the triggers (the trade market's range, "+1 [cruceta derecha]"): with a
            // text field focused, digits changing in its window are its value, each step said over the last. Not tied to a
            // press read from the pad: the first fix that was went unheard (user 2026-11-17, twice: "no lee los números").
            var field = this.inGame && focus?.Role == UiRole.TextField;
            if (field)
            {
                // Rare (a field focused and a number moving), and the only trace of why a step went unsaid.
                this.log.Info($"Numbers changed with a field focused: layer 0x{change.Layer.Address.ToInt64():X}, focus layer 0x{focusLayer.ToInt64():X}: " +
                              string.Join(" | ", change.Texts.Select((text, i) => $"{(i < change.Before.Count ? change.Before[i] : "?")} -> {text.Text}")));
            }

            if (!this.inGame || focusLayer == IntPtr.Zero || change.Layer.Address != focusLayer || change.Layer.Address == this.HudLayer)
            {
                return;
            }

            var now = Environment.TickCount64;
            var sinceInput = field ? 0 : this.SinceConfirm?.Invoke() ?? long.MaxValue;
            for (var i = 0; i < change.Texts.Count && i < change.Before.Count; i++)
            {
                var text = change.Texts[i].Text;
                if (focus != null && (text == focus.Text || text == focus.Value))
                {
                    continue; // the focused control's own value is the focus tracker's to say
                }

                if (NumberChanges.Said(change.Before[i], text, sinceInput, now - this.numberSaidAt, field) is { } line)
                {
                    this.numberSaidAt = now;
                    this.Speak(field ? "value stepped" : "number changed after a press", line, field);
                    return;
                }
            }
        }
    }
}
