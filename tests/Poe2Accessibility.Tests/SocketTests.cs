namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A piece of gear's sockets and the lines its runes give (docs/research/item-mods.md, "Sockets and runes").</summary>
    public static class SocketTests
    {
        private const string IronRune = "Metadata/Items/SoulCores/RuneEnhanceLesser", StormRune = "Metadata/Items/SoulCores/RuneLightningLesser";

        // Chainsting with a Lesser Iron Rune, as the play log of 2026-11-16 read it (the name, said first, left out).
        private static readonly string[] Chainsting =
        {
            "Lanza de cacería", "Lanza", "Calidad: +4%", "Daño físico: 30-50", "Probabilidad de impacto crítico: 5.00%", "Ataques por segundo: 1.71",
            "Requiere: Nivel 10, 9 Fue, 17 Des", "Daño físico aumentado un 14%", "Otorga la habilidad: Lanzamiento de lanza", "24% de probabilidad de lisiar al impactar",
            "Agrega de 15 a 25 de daño físico", "+136 a la precisión", "Velocidad de ataque aumentada un 10%",
        };

        // Its modifiers' own lines (the certain ones), as dev itemmodscheck found them equal to the tooltip.
        private static readonly string[] ChainstingMods = { "24% de probabilidad de lisiar al impactar", "Agrega de 15 a 25 de daño físico", "+136 a la precisión", "Velocidad de ataque aumentada un 10%" };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheShippedCatalogKnowsTheRunesSeen), TheShippedCatalogKnowsTheRunesSeen),
            (nameof(ARunesLineIsFoundInTheTooltip), ARunesLineIsFoundInTheTooltip),
            (nameof(TwoAlikeRunesShowTheirSum), TwoAlikeRunesShowTheirSum),
            (nameof(AModifiersLineEqualToARunesIsNotTaken), AModifiersLineEqualToARunesIsNotTaken),
            (nameof(TheSocketsAreSaidWithWhatFillsThem), TheSocketsAreSaidWithWhatFillsThem),
        };

        private static (ModStatCatalog Stats, ModTextCatalog Texts)? Shipped() =>
            FileCatalogTests.Shipped("mod-stats.json") is { } stats && FileCatalogTests.Shipped("mod-texts.json") is { } texts
                ? (ModStatCatalog.Parse(File.ReadAllText(stats)), ModTextCatalog.FromJson(File.ReadAllText(texts)))
                : null;

        private static IEnumerable<string> Given(IReadOnlyList<string> runes, IReadOnlyList<string> tooltip, IEnumerable<string> mods, (ModStatCatalog Stats, ModTextCatalog Texts) catalogs) =>
            SocketedRunes.LinesGiven(runes, tooltip, mods, "es", catalogs.Stats, catalogs.Texts).Select(at => tooltip[at]);

        private static void TheShippedCatalogKnowsTheRunesSeen()
        {
            if (Shipped() is not { } catalogs)
            {
                return; // away from the repository
            }

            var iron = catalogs.Stats.RuneStatsOf(IronRune);
            Equal(true, iron.Values.Any(stats => stats.SequenceEqual(new[] { ("local_physical_damage_+%", 14) })), "the iron rune gives 14% physical damage in one category (Martial Weapon)");
            Equal(true, catalogs.Stats.RuneStatsOf(StormRune).Values.Any(stats => stats.SequenceEqual(new[] { ("local_minimum_added_lightning_damage", 1), ("local_maximum_added_lightning_damage", 10) })),
                "the storm rune adds 1 to 10 lightning damage, as its own tooltip says");
            Equal(0, catalogs.Stats.RuneStatsOf("Metadata/Items/Rings/FourRing1").Count, "a ring is no rune");
        }

        private static void ARunesLineIsFoundInTheTooltip()
        {
            if (Shipped() is not { } catalogs)
            {
                return;
            }

            Equal("Daño físico aumentado un 14%", string.Join(" | ", Given(new[] { IronRune }, Chainsting, ChainstingMods, catalogs)), "Chainsting's rune line, the one its Mods lists lack");
            Equal(string.Empty, string.Join(" | ", Given(new[] { StormRune }, Chainsting, ChainstingMods, catalogs)), "a rune that is not the one socketed gives none of its lines");
            Equal(string.Empty, string.Join(" | ", Given(Array.Empty<string>(), Chainsting, ChainstingMods, catalogs)), "no rune, no line");
        }

        private static void TwoAlikeRunesShowTheirSum()
        {
            if (Shipped() is not { } catalogs)
            {
                return;
            }

            var tooltip = Chainsting.Select(line => line == "Daño físico aumentado un 14%" ? "Daño físico aumentado un 28%" : line).ToList();
            Equal("Daño físico aumentado un 28%", string.Join(" | ", Given(new[] { IronRune, IronRune }, tooltip, ChainstingMods, catalogs)), "two iron runes are one line of their sum");
        }

        private static void AModifiersLineEqualToARunesIsNotTaken()
        {
            if (Shipped() is not { } catalogs)
            {
                return;
            }

            var mods = ChainstingMods.Append("Daño físico aumentado un 14%").ToList();
            Equal(string.Empty, string.Join(" | ", Given(new[] { IronRune }, Chainsting, mods, catalogs)), "shown once and made by a modifier: the modifier's");
            var twice = Chainsting.Append("Daño físico aumentado un 14%").ToList();
            Equal("7", string.Join(" ", SocketedRunes.LinesGiven(new[] { IronRune }, twice, mods, "es", catalogs.Stats, catalogs.Texts)), "shown twice: the first, where runes go, is the rune's");
        }

        private static void TheSocketsAreSaidWithWhatFillsThem()
        {
            Localization.Set("es", new());
            try
            {
                Equal("1 engarce: Runa de hierro menor. Efecto: Daño físico aumentado un 14%", Announcements.Sockets(new[] { "Runa de hierro menor" }, new[] { "Daño físico aumentado un 14%" }), "one rune and its line");
                Equal("2 engarces: Runa de hierro menor, vacío", Announcements.Sockets(new string?[] { "Runa de hierro menor", null }, Array.Empty<string>()), "an empty socket, and no line found");
            }
            finally
            {
                Localization.Set("en", new());
            }

            Equal("1 socket: empty", Announcements.Sockets(new string?[] { null }, Array.Empty<string>()), "in English");
        }
    }
}
