namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A landed Atlas node's words (<see cref="UiSemantics.AtlasNodeLine"/>): the whole landing line, and that line
    ///     less what the node's popup already said. The screen reading says the popup first — title, biome line,
    ///     flavour — so the landing line repeated the name and biome on every hop (the owner, 2026-11-28).
    /// </summary>
    /// <param name="BiomeLine">The popup's own biome line ("Biome: Forest"), as the screen reading said it; empty when it has none.</param>
    public sealed record AtlasLandingWords(string Name, string State, string BiomeLine, string Biome, string Contents, string Links)
    {
        /// <summary>No endgame node under the cursor: nothing to say.</summary>
        public static readonly AtlasLandingWords None = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        /// <summary>The whole line: name, state, biome, contents, links; empty for no node.</summary>
        public string Text => this.Spoken(_ => false);

        /// <summary>The line without the name and the biome when <paramref name="said"/> tells they were just heard.</summary>
        public string Spoken(Func<string, bool> said) => this.Name.Length == 0
            ? string.Empty
            : Announcements.AtlasLanding(
                said(this.Name) ? string.Empty : this.Name,
                this.State,
                this.BiomeLine.Length > 0 && said(this.BiomeLine) ? string.Empty : this.Biome,
                this.Contents,
                this.Links);
    }
}
