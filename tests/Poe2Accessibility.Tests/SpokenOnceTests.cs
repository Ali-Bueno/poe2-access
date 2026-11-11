namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>What must not reach speech: an internal name in a warning, and a chat history already said.</summary>
    public static class SpokenOnceTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APartWithNoWordingIsNotSaidByItsInternalName), APartWithNoWordingIsNotSaidByItsInternalName),
            (nameof(AChatThatReadEmptyOnceIsNotSaidAgainWhole), AChatThatReadEmptyOnceIsNotSaidAgainWhole),
            (nameof(AChatWhoseNewestLineIsItsOldestIsNotSaidAgainWhole), AChatWhoseNewestLineIsItsOldestIsNotSaidAgainWhole),
        };

        /// <summary>"Aviso de compatibilidad: no encuentro EntityOffsets" was heard on 2026-11-08; "passive build marks" would be today.</summary>
        private static void APartWithNoWordingIsNotSaidByItsInternalName()
        {
            var said = Announcements.CompatibilityMissing(new[] { "passive build marks", "dat keys", "life" });
            Equal(false, said.Contains("passive build marks", StringComparison.Ordinal) || said.Contains("dat keys", StringComparison.Ordinal), "an internal name stays in the log");
            Equal(Announcements.CompatibilityMissing(new[] { "code fields", "life" }), said, "such parts are said once, as game data that may read wrong, beside the parts that have their own words");
        }

        /// <summary>One poll taken while the game rebuilds its chat lines reads none; the next one reads them all back.</summary>
        private static void AChatThatReadEmptyOnceIsNotSaidAgainWhole()
        {
            const int Fresh = 5;
            var history = new[] { "Una: Bienvenido.", "Intercambio aceptado.", "Renly: Vuelve pronto.", "yarkey: hola", "Una: Adiós.", "Intercambio cancelado." };
            Equal(0, ChatDiff.News(Array.Empty<string>(), history, Fresh).Count, "the history back after an empty reading is not news");
            Equal("Una: Bienvenido.", string.Join(" | ", ChatDiff.News(Array.Empty<string>(), new[] { "Una: Bienvenido." }, Fresh)), "the first lines of an empty chat are");
            Equal("Intercambio cancelado.", string.Join(" | ", ChatDiff.News(history[..5], history, Fresh)), "and so is a line added to a chat that read whole");
        }

        /// <summary>A poll that read only the newest line, the same shout as the oldest one, lined up with the whole history (2026-11-11).</summary>
        private static void AChatWhoseNewestLineIsItsOldestIsNotSaidAgainWhole()
        {
            const int Fresh = 5;
            var shout = "yarkey: ¡Todavía no!";
            var history = new[] { shout, "yarkey: ¡Locos!", "yarkey ha muerto.", "Renly: Vuelve pronto.", "yarkey: ¡Granada!", "Una: Adiós.", "yarkey: ¡Más tiempo!", shout };
            Equal(0, ChatDiff.News(new[] { shout }, history, Fresh).Count, "the history back after a reading of its newest line only is not news");
            Equal("Una: Hola. | yarkey: ¡Ya!", string.Join(" | ", ChatDiff.News(history, history.Append("Una: Hola.").Append("yarkey: ¡Ya!").ToArray(), Fresh)), "lines added to a chat that read whole still are");
        }
    }
}
