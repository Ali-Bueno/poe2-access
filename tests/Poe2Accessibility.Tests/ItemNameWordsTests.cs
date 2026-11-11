namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A rare or unique item's name from the Words.dat rows it keeps (<see cref="ItemNameWords"/>); the rows are yarkey's worn items, read live on 2026-11-10.</summary>
    public static class ItemNameWordsTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AUniqueIsItsOneWord), AUniqueIsItsOneWord),
            (nameof(ARareIsItsTwoWordsInTheOrderTheSpaceGives), ARareIsItsTwoWordsInTheOrderTheSpaceGives),
            (nameof(WordsThatDoNotSayTheNameGiveNone), WordsThatDoNotSayTheNameGiveNone),
            (nameof(TitlesThatDisagreeEndTheTrust), TitlesThatDisagreeEndTheTrust),
            (nameof(AGameRestartedPatchedIsNoticed), AGameRestartedPatchedIsNoticed),
        };

        private static void AGameRestartedPatchedIsNoticed()
        {
            Equal(false, GameBuildChange.Patched(string.Empty, "100:5"), "the first game process under the mod");
            Equal(false, GameBuildChange.Patched("100:5", "100:5"), "the same exe again");
            Equal(true, GameBuildChange.Patched("100:5", "120:9"), "another exe: the game data is the last build's");
            Equal(false, GameBuildChange.Patched("100:5", string.Empty), "an exe that could not be read is no news");
        }

        private static ItemNameWords.Word Word(string text, string inflection = "") => new(text, inflection);

        private static void AUniqueIsItsOneWord()
        {
            Equal("Estallido repentino", ItemNameWords.Compose(new[] { Word("Estallido repentino") }), "the unique ring");
            Equal("Trenza de medianoche", ItemNameWords.Compose(new[] { Word("<<set:FS>>Trenza de medianoche", "FS") }), "a marker shows nothing");
        }

        private static void ARareIsItsTwoWordsInTheOrderTheSpaceGives()
        {
            const string Chimeric = "<if:MS>{ quimérico}<elif:FS>{ quimérica}<elif:MP>{ quiméricos}<elif:FP>{ quiméricas}";
            Equal("Filo quimérico", ItemNameWords.Compose(new[] { Word(Chimeric), Word("Filo") }), "Spanish: the noun first, no inflection is the first branch");
            Equal("Cuentas quiméricas", ItemNameWords.Compose(new[] { Word(Chimeric), Word("Cuentas", "FP") }), "the noun's inflection picks the branch");
            Equal("Cuentas de destino", ItemNameWords.Compose(new[] { Word(" de destino"), Word("Cuentas", "FP") }), "a word with no branches");
            Equal("Estrella quimérica", ItemNameWords.Compose(new[] { Word(Chimeric), Word("Estrella", "F") }), "a singular noun's row gives its gender alone (malgdhler's helm, 2026-11-10)");
            Equal("Banda\u00A0de kraken", ItemNameWords.Compose(new[] { Word("\u00A0de kraken"), Word("Banda", "F") }), "the space may be a no-break one, kept as written");
            Equal("Chimeric Edge", ItemNameWords.Compose(new[] { Word("Chimeric"), Word(" Edge") }), "English: the space is in the second word");
            Equal(" quimérica", ItemNameWords.Resolve(Chimeric, "fs"), "a branch by its key");
            Equal(" quimérico", ItemNameWords.Resolve(Chimeric, "N"), "an unknown key: the first branch");
        }

        private static void WordsThatDoNotSayTheNameGiveNone()
        {
            Equal(string.Empty, ItemNameWords.Compose(Array.Empty<ItemNameWords.Word>()), "a magic item keeps no words");
            Equal(string.Empty, ItemNameWords.Compose(new[] { Word("a"), Word(" b"), Word(" c") }), "three words are not this field");
            Equal(string.Empty, ItemNameWords.Compose(new[] { Word("キメラ"), Word("刃") }), "no space in either: the order is not known");
            Equal(string.Empty, ItemNameWords.Compose(new[] { Word(" de x"), Word(" de y") }), "a space in both");
            Equal(string.Empty, ItemNameWords.Compose(new[] { Word("Filo"), Word(string.Empty) }), "a word that did not read");
            Equal(string.Empty, ItemNameWords.Compose(new[] { Word("<b>{Filo") }), "markup the rule does not know is never said");
        }

        private static void TitlesThatDisagreeEndTheTrust()
        {
            var trust = new ItemNameTrust();
            Equal(false, trust.Observe(string.Empty, "Filo quimérico"), "no words: nothing judged");
            Equal(false, trust.Observe("Filo quimérico", "Filo quimérico"), "a title that agrees");
            Equal(false, trust.Observe("Filo quimérico", "Otro nombre"), "one that differs");
            Equal(false, trust.Lost, "one against one: kept");
            Equal(true, trust.Observe("x", "y"), "two against one: this one ends it");
            Equal(true, trust.Lost, "more titles differ than agree: given up");

            var moved = new ItemNameTrust();
            moved.Observe("a", "b");
            Equal(true, moved.Observe("c", "d"), "two titles differ and none agrees: lost, said once");
            Equal(false, moved.Observe("e", "f"), "already lost");
        }
    }
}
