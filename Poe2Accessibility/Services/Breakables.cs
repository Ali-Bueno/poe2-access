namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A breakable's material by its metadata path, naming its sound in <c>sounds/breakables/</c>. The game's own
    ///     link: each <c>Metadata/Chests/**.ao</c> plays an FMOD event (<c>…/G2_2/Urns/Urn</c>, <c>…/Boulders/BouldersDirty/Open_2</c>)
    ///     whose bank holds the sample (<c>Poe2UiExtract ao-sounds</c>, 2026-10-27); crates have no bank of their own and
    ///     take a generic wood smash. More specific names first: a Titan Valley pot is big, an Ezomyte stone pot a statue.
    /// </summary>
    public static class Breakables
    {
        private static readonly (string Name, string Material)[] Materials =
        {
            ("titanvalleypots", "bigpot"),
            ("pillars", "bigpot"),
            ("ezomytestatue", "statue"),
            ("stonepot", "statue"),
            ("gobrockpile", "rubble"),
            ("boulder", "boulder"),
            ("bolder", "boulder"),
            ("rock", "boulder"),
            ("egg", "egg"),
            ("urn", "urn"),
            ("jug", "pot"),
            ("bottle", "pot"),
            ("pot", "pot"),
            ("barrel", "barrel"),
            ("bone", "bone"),
            ("crate", "crate"),
            ("logchest", "crate"),
            ("goblinchest", "crate"),
            ("basket", "basket"),
        };

        /// <summary>The material's sound name, or empty for the common breaking sound.</summary>
        public static string Material(string path)
        {
            foreach (var (name, material) in Materials)
            {
                if (path.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return material;
                }
            }

            return string.Empty;
        }
    }
}
