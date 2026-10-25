namespace Poe2Accessibility
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>An interpolated phrase kept as its composite format and arguments, so a translation can reorder them.</summary>
    [InterpolatedStringHandler]
    public sealed class Phrase
    {
        private readonly StringBuilder format;
        private readonly object?[] arguments;
        private int count;

        public Phrase(int literalLength, int formattedCount)
        {
            this.format = new StringBuilder(literalLength + (formattedCount * 4));
            this.arguments = new object?[formattedCount];
        }

        /// <summary>The composite format the phrase tables are keyed by: "Life {0} percent".</summary>
        public string Format => this.format.ToString();

        public void AppendLiteral(string text) => this.format.Append(text.Replace("{", "{{").Replace("}", "}}"));

        public void AppendFormatted<TValue>(TValue value) => this.Hole(value, null, null);

        public void AppendFormatted<TValue>(TValue value, string? format) => this.Hole(value, null, format);

        public void AppendFormatted<TValue>(TValue value, int alignment) => this.Hole(value, alignment, null);

        public void AppendFormatted<TValue>(TValue value, int alignment, string? format) => this.Hole(value, alignment, format);

        /// <summary>The phrase in the given composite format (a translation of <see cref="Format"/>), else as written.</summary>
        public string Text(string? translated = null)
        {
            if (translated != null)
            {
                try
                {
                    return string.Format(translated, this.arguments);
                }
                catch (FormatException)
                {
                    // A broken translation falls back to the English phrase.
                }
            }

            return string.Format(this.Format, this.arguments);
        }

        private void Hole(object? value, int? alignment, string? holeFormat)
        {
            this.format.Append('{').Append(this.count);
            if (alignment is { } width)
            {
                this.format.Append(',').Append(width);
            }

            if (!string.IsNullOrEmpty(holeFormat))
            {
                this.format.Append(':').Append(holeFormat);
            }

            this.format.Append('}');
            this.arguments[this.count++] = value;
        }
    }
}
