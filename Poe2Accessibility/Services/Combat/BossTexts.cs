namespace Poe2Accessibility.Services
{
    using System.Text.RegularExpressions;

    /// <summary>A buff's name or description from the game's tables, as it is spoken: without markup or keyword links. Pure.</summary>
    public static class BossTexts
    {
        /// <summary>A keyword link, "[Key|shown words]" or "[Word]": the shown words are kept.</summary>
        private static readonly Regex Link = new(@"\[(?:[^\[\]|]*\|)?([^\[\]|]*)\]", RegexOptions.Compiled);

        /// <summary>A format tag around its text, "&lt;red&gt;{text}": the text is kept.</summary>
        private static readonly Regex Wrapped = new(@"<[^<>{}]*>\{([^{}]*)\}", RegexOptions.Compiled);

        /// <summary>A tag alone, or a value the table leaves to the game ("{0}", "%1%").</summary>
        private static readonly Regex Stray = new(@"<[^<>]*>|\{\d*[^{}]*\}|%\d+%", RegexOptions.Compiled);

        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>"You are [Stun|stunned] by &lt;red&gt;{the storm}" → "You are stunned by the storm".</summary>
        public static string Plain(string text)
        {
            text = Link.Replace(text, "$1");
            string unwrapped;
            while ((unwrapped = Wrapped.Replace(text, "$1")) != text)
            {
                text = unwrapped;
            }

            return Spaces.Replace(Stray.Replace(text, string.Empty), " ").Trim();
        }

        /// <summary>A name and its description as one line; either alone when the other is empty or repeats it.</summary>
        public static string Line(string name, string description) =>
            description.Length == 0 || description == name ? name : name.Length == 0 ? description : $"{name}. {description}";
    }
}
