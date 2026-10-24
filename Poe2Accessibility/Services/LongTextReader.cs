namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     A text too long to say at once, scrolled inside its frame (the terms of use: ~60 000 characters, 21 060 layout
    ///     units tall in a 1 000-unit frame, 2026-10-24): its paragraph count when it shows, then the paragraph at the
    ///     scroll position each time scrolling brings another one, with its place ("12 de 98").
    /// </summary>
    public sealed class LongTextReader
    {
        private const int PollMs = 150;

        /// <summary>
        ///     Scrolling forward past at most this many paragraphs reads the ones passed too, in order: a short push of
        ///     the stick went from 67 to 70 (2026-10-24). A longer jump, or back, reads only where it lands.
        /// </summary>
        private const int ReadThroughParagraphs = 4;

        private readonly UiElementReader elements;
        private readonly IAccessibilityOutputService output;
        private IntPtr text;
        private string[] paragraphs = Array.Empty<string>();
        private int[] starts = Array.Empty<int>();
        private int total;
        private int spoken = -1;
        private long lastPoll;

        public LongTextReader(UiElementReader elements, IAccessibilityOutputService output)
        {
            this.elements = elements;
            this.output = output;
        }

        /// <summary>Per frame with the long text on screen (zero when there is none).</summary>
        public void Tick(IntPtr longText)
        {
            if (longText != this.text)
            {
                this.text = longText;
                this.spoken = -1;
                this.paragraphs = Array.Empty<string>();
                if (longText != IntPtr.Zero && this.elements.TryReadLongText(longText, out var whole))
                {
                    this.paragraphs = Paragraphs(whole);
                    this.starts = new int[this.paragraphs.Length];
                    this.total = 0;
                    for (var i = 0; i < this.paragraphs.Length; i++)
                    {
                        this.starts[i] = this.total;
                        this.total += this.paragraphs[i].Length;
                    }

                    if (this.paragraphs.Length > 0)
                    {
                        this.output.Speak(Announcements.LongText(this.paragraphs.Length), false);
                    }
                }
            }

            var now = Environment.TickCount64;
            if (this.paragraphs.Length == 0 || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (ScrollFraction(this.elements, this.text) is not { } fraction)
            {
                return;
            }

            var index = ParagraphAt(this.starts, (int)(fraction * this.total));
            if (index == this.spoken)
            {
                return;
            }

            // The first paragraph follows the screen's own lines; a paragraph scrolled to replaces the one being read.
            var first = this.spoken >= 0 && index > this.spoken && index - this.spoken <= ReadThroughParagraphs ? this.spoken + 1 : index;
            for (var paragraph = first; paragraph <= index; paragraph++)
            {
                this.output.Speak(Announcements.LongTextParagraph(this.paragraphs[paragraph], paragraph + 1, this.paragraphs.Length), this.spoken >= 0 && paragraph == first);
            }

            this.spoken = index;
        }

        /// <summary>The text's lines that hold words; the layout's blank lines only separate them.</summary>
        public static string[] Paragraphs(string whole) =>
            whole.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        /// <summary>The paragraph holding character <paramref name="offset"/>, from each paragraph's first character.</summary>
        public static int ParagraphAt(IReadOnlyList<int> starts, int offset)
        {
            var index = 0;
            while (index + 1 < starts.Count && starts[index + 1] <= offset)
            {
                index++;
            }

            return index;
        }

        /// <summary>
        ///     How far the frame has scrolled, 0 at the top and 1 at the bottom: the frame's position modifier is the
        ///     scroll (negative, layout units), against the text's height less the frame's.
        /// </summary>
        private static float? ScrollFraction(UiElementReader elements, IntPtr text)
        {
            if (!elements.TryReadElement(text, out var info) || !elements.TryReadElement(info.Raw.ParentPtr, out var frame))
            {
                return null;
            }

            var range = info.Raw.UnscaledSize.Y - frame.Raw.UnscaledSize.Y;
            return range <= 0 ? 0f : Math.Clamp(-frame.Raw.PositionModifier.Y / range, 0f, 1f);
        }
    }
}
