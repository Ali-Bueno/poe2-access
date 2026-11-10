namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Access;
    using static Poe2Accessibility.Tests.DatColumnSearchTests;
    using static Check;

    /// <summary>The spoken game-data update (language, phrases, pacing) and the telegraph tables' columns found by content.</summary>
    public static class StartupSpeechTests
    {
        private const int GeometryRowSize = 0x152;
        private const int TriggerRowSize = 0xEA;
        private const int MortarRowSize = 0x88;
        private const long NoKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheSettingsLanguageComesFirst), TheSettingsLanguageComesFirst),
            (nameof(AutoFollowsTheGameThenTheSystem), AutoFollowsTheGameThenTheSystem),
            (nameof(SpanishAndEnglishAreInCodeTheRestInTheTable), SpanishAndEnglishAreInCodeTheRestInTheTable),
            (nameof(ProgressIsSaidOncePerIntervalWhenItMoved), ProgressIsSaidOncePerIntervalWhenItMoved),
            (nameof(TelegraphColumnsAreFoundWhereMeasured), TelegraphColumnsAreFoundWhereMeasured),
            (nameof(TelegraphColumnsFollowAShift), TelegraphColumnsFollowAShift),
            (nameof(AWrongKnownValueMakesTheColumnMissing), AWrongKnownValueMakesTheColumnMissing),
        };

        private static void TheSettingsLanguageComesFirst()
        {
            Expect("fr", StartupPhrases.ResolveLanguage("{ \"Language\": \"fr\" }", "spanish", "en-US"), "the setting wins");
            Expect("zh-TW", StartupPhrases.ResolveLanguage("{ \"Language\": \"zh_CN\" }", null, "en-US"), "a code is normalised");
            Expect("en", StartupPhrases.ResolveLanguage("[]", null, "en-US"), "settings that are not an object are ignored");
        }

        private static void AutoFollowsTheGameThenTheSystem()
        {
            Expect("es", StartupPhrases.ResolveLanguage("{ \"Language\": \"auto\" }", "es", "en-US"), "auto: the game's language");
            Expect("de", StartupPhrases.ResolveLanguage(null, null, "de-DE"), "no setting, no game: the system's");
            Expect("pt", StartupPhrases.ResolveLanguage("{ not json", "pt", "en"), "unreadable settings: the game's");
            Expect("en", StartupPhrases.ResolveLanguage(null, null, "xx-YY"), "nothing known: English");
        }

        private static void SpanishAndEnglishAreInCodeTheRestInTheTable()
        {
            var table = new Dictionary<string, string> { [StartupPhrases.Ready.English] = "Données prêtes" };
            Expect("Datos del juego listos", StartupPhrases.Text(StartupPhrases.Ready, "es", table), "Spanish in code");
            Expect("Game data ready", StartupPhrases.Text(StartupPhrases.Ready, "en", table), "English in code");
            Expect("Données prêtes", StartupPhrases.Text(StartupPhrases.Ready, "fr", table), "the table by the English key");
            Expect(StartupPhrases.Starting.English, StartupPhrases.Text(StartupPhrases.Starting, "fr", table), "a missing entry is English");
            Expect("Datos del juego: 8 de 22", StartupPhrases.Format(StartupPhrases.Progress, "es", null, 8, 22), "progress");
        }

        private static void ProgressIsSaidOncePerIntervalWhenItMoved()
        {
            var pacer = new ProgressPacer(TimeSpan.FromSeconds(30));
            var start = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
            pacer.Start(start);
            Expect(false, pacer.Due(0, start.AddSeconds(10)), "too soon after the opening line");
            Expect(true, pacer.Due(3, start.AddSeconds(31)), "a full interval and the count moved");
            Expect(false, pacer.Due(4, start.AddSeconds(40)), "inside the interval");
            Expect(false, pacer.Due(3, start.AddSeconds(70)), "the count did not move");
            Expect(true, pacer.Due(8, start.AddSeconds(71)), "moved again after the interval");
        }

        private static DatTable Geometry(int shift)
        {
            var builder = new DatTableBuilder(GeometryRowSize + shift);
            builder.Row(w => w.Text(0, "cone").Int(TelegraphColumns.GeometryRangeOffset + shift, 85).Int(TelegraphColumns.GeometryShapeOffset + shift, 2).Int(TelegraphColumns.GeometryLengthOffset + shift, 10)
                .Int(TelegraphColumns.GeometryAngleOffset + shift, 100).Int(TelegraphColumns.GeometryDelayOffset + shift, 33));
            builder.Row(w => w.Text(0, "rectangle").Int(TelegraphColumns.GeometryShapeOffset + shift, 3).Int(TelegraphColumns.GeometryLengthOffset + shift, 10).Int(TelegraphColumns.GeometryWidthOffset + shift, 20));
            builder.Row(w => w.Text(0, "circle").Int(TelegraphColumns.GeometryShapeOffset + shift, 1).Int(TelegraphColumns.GeometryLengthOffset + shift, 21).Int(TelegraphColumns.GeometryRangeOffset + shift, 264));
            return builder.Build();
        }

        private static DatTable Triggers(int shift) => new DatTableBuilder(TriggerRowSize + shift)
            .Row(w => w.Text(0, "trigger").Int(TelegraphColumns.TriggerCountOffset + shift, 1).Int(TelegraphColumns.TriggerRangeOffset + shift, 45)
                .Int(TelegraphColumns.TriggerRadiusOffset + shift, 40).Int(TelegraphColumns.TriggerAngleOffset + shift, 360))
            .Row(w => w.Text(0, "other").Int(TelegraphColumns.TriggerCountOffset + shift, 3).Int(TelegraphColumns.TriggerRangeOffset + shift, 15)
                .Int(TelegraphColumns.TriggerRadiusOffset + shift, 10).Int(TelegraphColumns.TriggerAngleOffset + shift, 90)).Build();

        private static DatTable Mortars(int shift) => new DatTableBuilder(MortarRowSize + shift)
            .Row(w => w.Text(0, "mortar").Key(TelegraphColumns.MortarImpactOffset + shift, 1)).Row(w => w.Text(0, "none").Key(TelegraphColumns.MortarImpactOffset + shift, NoKey)).Build();

        private static DatTable Animated() => new DatTableBuilder(0x10).Row(w => w.Text(0, "a")).Row(w => w.Text(0, "b")).Build();

        private static readonly TelegraphColumns.KnownRows Known = new(0, 1, 0);

        private static void TelegraphColumnsAreFoundWhereMeasured()
        {
            var c = TelegraphColumns.Find(Geometry(0), Triggers(0), Mortars(0), Animated(), Known) ?? throw new Exception("not found");
            Expect((0x24, 0x28, 0x30, 0x34, 0x71, 0x7A), (c.GeometryRange, c.GeometryShape, c.GeometryLength, c.GeometryWidth, c.GeometryAngle, c.GeometryDelay), "geometry");
            Expect((0x24, 0x28, 0x34, 0x38, 0x3C, 0x24), (c.TriggerCountColumn, c.TriggerDelay, c.TriggerRangeColumn, c.TriggerRadiusColumn, c.TriggerAngleColumn, c.MortarImpact), "triggers and mortars");
        }

        private static void TelegraphColumnsFollowAShift()
        {
            const int shift = 0x10;
            var c = TelegraphColumns.Find(Geometry(shift), Triggers(shift), Mortars(shift), Animated(), Known) ?? throw new Exception("not found");
            Expect((0x24 + shift, 0x28 + shift, 0x30 + shift, 0x34 + shift, 0x71 + shift, 0x7A + shift), (c.GeometryRange, c.GeometryShape, c.GeometryLength, c.GeometryWidth, c.GeometryAngle, c.GeometryDelay), "geometry");
            Expect((0x24 + shift, 0x34 + shift, 0x38 + shift, 0x3C + shift, 0x24 + shift), (c.TriggerCountColumn, c.TriggerRangeColumn, c.TriggerRadiusColumn, c.TriggerAngleColumn, c.MortarImpact), "triggers and mortars");
        }

        private static void AWrongKnownValueMakesTheColumnMissing()
        {
            var wrong = new TelegraphColumns.KnownRows(2, 1, 0); // row 2 is a circle, not the cone the values describe
            Expect(true, TelegraphColumns.Find(Geometry(0), Triggers(0), Mortars(0), Animated(), wrong) == null, "no column holds the cone's values on that row");
        }
    }
}
