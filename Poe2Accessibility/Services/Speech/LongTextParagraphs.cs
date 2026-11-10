namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>The pure part of <see cref="LongTextReader"/>: how a long text splits into paragraphs and where a scroll lands.</summary>
    public static class LongTextParagraphs
    {
        /// <summary>The text's lines that hold words; the layout's blank lines only separate them.</summary>
        public static string[] Split(string whole) =>
            whole.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        /// <summary>The paragraph holding character <paramref name="offset"/>, from each paragraph's first character.</summary>
        public static int At(IReadOnlyList<int> starts, int offset)
        {
            var index = 0;
            while (index + 1 < starts.Count && starts[index + 1] <= offset)
            {
                index++;
            }

            return index;
        }
    }
}
