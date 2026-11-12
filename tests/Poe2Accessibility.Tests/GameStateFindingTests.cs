namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;
    using static ScreenFixtures;

    /// <summary>Fields and flags found from live data, the tile grid, known places, scrolled rows and kept lessons.</summary>
    public static class GameStateFindingTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(InPlayFlagIsFoundFromStoryVariants), InPlayFlagIsFoundFromStoryVariants),
            (nameof(MovedFieldsAreFoundByWatchingThemChange), MovedFieldsAreFoundByWatchingThemChange),
            (nameof(FlagBitsAreFoundOnTheirCarriers), FlagBitsAreFoundOnTheirCarriers),
            (nameof(PadButtonsFollowTheLeftTrigger), PadButtonsFollowTheLeftTrigger),
            (nameof(TileGridIsFoundByWhatItsRecordsHold), TileGridIsFoundByWhatItsRecordsHold),
            (nameof(BaseNamesAreCheckedAgainstTooltipTitles), BaseNamesAreCheckedAgainstTooltipTitles),
            (nameof(KnownPlacesOutliveTheSession), KnownPlacesOutliveTheSession),
            (nameof(InlineItemArtIsNamed), InlineItemArtIsNamed),
            (nameof(WrappedLabelsJoin), WrappedLabelsJoin),
            (nameof(ScrolledRowsAreNotNews), ScrolledRowsAreNotNews),
            (nameof(FocusDetailsAreNotScrolledRows), FocusDetailsAreNotScrolledRows),
            (nameof(KeptLessonIsHeardWhole), KeptLessonIsHeardWhole),
            (nameof(KeptLessonHoldsFromItsStart), KeptLessonHoldsFromItsStart),
            (nameof(PlayerMoveCutsAMessage), PlayerMoveCutsAMessage),
            (nameof(ANoticesCopyInTheHintBoxIsNoLesson), ANoticesCopyInTheHintBoxIsNoLesson),
            (nameof(AMessageCuttingAMoveIsFollowedByIt), AMessageCuttingAMoveIsFollowedByIt),
            (nameof(PendingLessonIsRemindedNowAndThen), PendingLessonIsRemindedNowAndThen),
        };

        private static void InPlayFlagIsFoundFromStoryVariants()
        {
            // Byte 0x10 is the flag; 0x11 is 1 on everything (no zero to tell it apart); 0x12 differs between Renly's variants
            // but is set on both.
            static byte[] Block(byte flag, byte variant) { var block = new byte[0x20]; block[0x10] = flag; block[0x11] = 1; block[0x12] = variant; return block; }
            var inPlay = new[] { Block(1, 1), Block(1, 1), Block(1, 1), Block(1, 1) };
            var renly = new IReadOnlyList<byte[]>[] { new[] { Block(1, 1), Block(0, 1) } };
            Equal(true, InPlayFlag.Fits(inPlay, renly, 0x10), "the flag fits");
            Equal(false, InPlayFlag.Fits(inPlay, renly, 0x11), "a byte that is 1 on everything does not");
            Equal(false, InPlayFlag.Fits(inPlay, renly, 0x12), "both variants in play: not the flag");
            Equal(false, InPlayFlag.Fits(inPlay, Array.Empty<IReadOnlyList<byte[]>>(), 0x10), "no story variants: nothing proves a 0");
            Equal(false, InPlayFlag.Fits(inPlay.Take(2).ToList(), renly, 0x10), "too little in play");
            Equal(true, InPlayFlag.QuestStateHolds(inPlay, 0x11), "the quest flag is believed where all that is surely in play reads 1");
            Equal(false, InPlayFlag.QuestStateHolds(inPlay, 0x13), "not where one of them reads 0: nothing is left out by it");
            Equal(false, InPlayFlag.QuestStateHolds(inPlay.Take(2).ToList(), 0x11), "nor on too little in play");
            Equal(0x10, OffsetSearch.First(0x14, 8, 1, at => InPlayFlag.Fits(inPlay, renly, at)), "found again from a moved start");
            Equal(1.0, InPlayFlag.Share(inPlay, 0x10), "share in play");
        }

        private static void MovedFieldsAreFoundByWatchingThemChange()
        {
            static Dictionary<int, long> Read(long atZero, long atEight, long counter) => new() { [0] = atZero, [8] = atEight, [16] = counter };
            static bool Index(long value) => value is >= 0 and < 4;
            var control = (IntPtr)0x1000;

            var measured = new ChangingField();
            measured.Observe(control, 0, Read(0, 3, 100), Index);
            Equal(true, measured.Observe(control, 0, Read(1, 3, 101), Index), "the expected field changing settles it");
            Equal(0, measured.Proved, "where measured");

            var moved = new ChangingField();
            moved.Observe(control, 0, Read(0, 0, 100), Index);
            Equal(false, moved.Observe(control, 0, Read(0, 1, 101), Index), "one change is not enough");
            Equal(true, moved.Observe(control, 0, Read(0, 2, 102), Index), "a second change elsewhere moves it");
            Equal(8, moved.Proved, "the field that moved with the selection (a counter out of range never counts)");

            var other = new ChangingField();
            other.Observe(control, 0, Read(0, 0, 100), Index);
            other.Observe(control, 0, Read(0, 1, 100), Index);
            Equal(false, other.Observe((IntPtr)0x2000, 0, Read(0, 2, 100), Index), "a new control starts over");
            Equal(null, other.Proved, "nothing proved yet");
        }

        private static void FlagBitsAreFoundOnTheirCarriers()
        {
            // Bit 11 (visible) is on everything; bit 27 marks the controls; one label wrongly carries it.
            const uint visible = 1u << 11, control = 1u << 27;
            var controls = Enumerable.Repeat(visible | control, 20).ToList();
            var labels = Enumerable.Repeat(visible, 19).Append(visible | control).ToList();
            Equal(27, BitSearch.FindBit(controls, labels, 27, 0.9, 0.1, 3), "where measured, a stray label tolerated");
            Equal(27, BitSearch.FindBit(controls, labels, 22, 0.9, 0.1, 3), "found from a moved start; the visible bit never fits");
            Equal(null, BitSearch.FindBit(controls.Take(2).ToList(), labels, 27, 0.9, 0.1, 3), "too few to judge");
            Equal(0.05, BitSearch.Share(labels, 27), "share");
        }

        private static void PadButtonsFollowTheLeftTrigger()
        {
            static PadButtonCheck Feed(int triggerBit, uint alsoHeld)
            {
                var check = new PadButtonCheck();
                for (var frame = 0; frame < 25; frame++)
                {
                    check.Observe((1u << triggerBit) | alsoHeld, 1f, 0f, 14);
                    check.Observe(alsoHeld, 0f, 0f, 14);
                    check.Observe(1u << 3, 0.4f, 0f, 14); // half pressed: not counted
                }

                return check;
            }

            Equal(0, Feed(14, 0).Shift, "where measured");
            Equal(2, Feed(16, 0).Shift, "every button moved by two");
            Equal(0, Feed(14, 1u << 10).Shift, "A held throughout is set both ways: not the trigger");
            var none = new PadButtonCheck();
            for (var frame = 0; frame < 25; frame++)
            {
                none.Observe(0, 1f, 0f, 14);
                none.Observe(0, 0f, 0f, 14);
            }

            Equal(true, none.Judged, "judged");
            Equal(null, none.Shift, "no bit follows the trigger");
        }

        private static void TileGridIsFoundByWhatItsRecordsHold()
        {
            // Six rows of ten sub-tiles; each tile spans five sub-tiles of two rows, its file at 0x1000 + 0x100 per tile.
            const int perRow = 10;
            const int subTiles = 60;
            static byte[] Grid(TileGridSearch.Layout layout, bool ids)
            {
                var noise = new Random(7);
                var grid = new byte[subTiles * layout.Stride];
                noise.NextBytes(grid);
                for (var i = 0; i < subTiles; i++)
                {
                    var (row, column) = (i / perRow, i % perRow);
                    var tile = ((row / 2) * 2) + (column / 5);
                    var at = i * layout.Stride;
                    BitConverter.GetBytes(0x1000L + (tile * 0x100)).CopyTo(grid, at + layout.FileOffset);
                    (grid[at + layout.IdsOffset], grid[at + layout.IdsOffset + 1], grid[at + layout.IdsOffset + 2]) =
                        ids ? ((byte)(column % 5), (byte)(row % 2), (byte)(tile % 8)) : ((byte)0, (byte)0, (byte)0);
                }

                return grid;
            }

            static bool NamesTile(long file, int path) => path == 0x10 && file is >= 0x1000 and < 0x2000;
            var measured = new TileGridSearch.Layout(0x38, 0x8, 0x10, 0x34);
            var moved = new TileGridSearch.Layout(0x40, 0x10, 0x10, 0x3A);
            Equal(measured, TileGridSearch.Find(Grid(measured, true), subTiles, perRow, measured, NamesTile), "where measured");
            Equal(moved, TileGridSearch.Find(Grid(moved, true), subTiles, perRow, measured, NamesTile), "a longer record with its fields moved");
            Equal(null, TileGridSearch.Find(Grid(measured, false), subTiles, perRow, measured, NamesTile), "no bytes step between neighbours");
            Equal(null, TileGridSearch.Find(Grid(measured, true), subTiles + 1, perRow, measured, NamesTile), "bytes that are not whole records");
            Equal((0x38, 0x40), TileGridSearch.FirstMoved(measured, moved), "the record size is reported first");
        }

        private static void BaseNamesAreCheckedAgainstTooltipTitles()
        {
            static Func<int, int, string> NameAt(int row, int name) => (r, n) => (r, n) == (row, name) ? "Broquel de cuero" : (r, n) == (0x10, 0x40) ? "de" : string.Empty;
            string[] titles = { "Broquel de cuero", "Sólido broquel de cuero del zorro", "Broquel de cuero", "Broquel de cuero" };

            var kept = new BaseNameField(0x10, 0x30);
            Equal(false, kept.Observe(1, titles[0], NameAt(0x10, 0x30)), "one title is not enough");
            Equal(false, kept.Observe(1, titles[0], NameAt(0x10, 0x30)), "the same item polled again counts once");
            Equal(true, kept.Observe(2, titles[1], NameAt(0x10, 0x30)), "a magic title holds the base name, whatever its case");
            Equal(false, kept.Moved, "where measured");

            var moved = new BaseNameField(0x10, 0x30);
            var settled = titles.Select((title, item) => moved.Observe(item, title, NameAt(0x18, 0x38))).Count(done => done);
            Equal(1, settled, "settled once");
            Equal((0x18, 0x38), moved.Offsets, "found after two misses and a second title");
            Equal(true, moved.Moved, "moved");

            var polled = new BaseNameField(0x10, 0x30);
            for (var i = 0; i < 20; i++)
            {
                polled.Observe(1, "Broquel de cuero", NameAt(0x100, 0x100));
            }

            Equal(false, polled.Judged, "one item polled for seconds settles nothing");

            var lost = new BaseNameField(0x10, 0x30);
            for (var item = 0; item < BaseNameField.MissesToGiveUp; item++)
            {
                lost.Observe(item, "Broquel de cuero", NameAt(0x100, 0x100)); // "de" at +0x40 is too short to count
            }

            Equal(true, lost.Lost, "no place holds the name");
        }

        private static void KnownPlacesOutliveTheSession()
        {
            var file = System.IO.Path.GetTempFileName();
            try
            {
                var first = new KnownPlaces(file);
                Equal(true, first.Add("kehgan", "camp|waypoint|Transporter|0|0"), "a place is new the first time");
                Equal(false, first.Add("kehgan", "camp|waypoint|Transporter|0|0"), "then known");
                var next = new KnownPlaces(file);
                Equal(false, next.Add("kehgan", "camp|waypoint|Transporter|0|0"), "still known in the next session");
                Equal(true, next.Add("yarkbruja", "camp|waypoint|Transporter|0|0"), "another character has not seen it");
            }
            finally
            {
                System.IO.File.Delete(file);
            }
        }

        private static void InlineItemArtIsNamed()
        {
            ItemNames.Set(new() { ["en"] = new() { ["CurrencyIdentification"] = "Scroll of Wisdom", ["CurrencyUpgradeToMagic"] = "Orb of Transmutation" } });
            const string identify = "Identify <<itemvisual:CurrencyIdentification>> x 1";
            Equal(true, ControllerGlyphs.HasUnnamedItemArt(identify), "the identify legend shows its scroll only as art");
            Equal("Identify Scroll of Wisdom x 1", ControllerGlyphs.Plain(identify), "the art is named in place");
            const string named = "Use an <<itemvisual:CurrencyUpgradeToMagic>> <currencyitem>{Orb of Transmutation}";
            Equal(false, ControllerGlyphs.HasUnnamedItemArt(named), "art followed by its own name is not named twice");
            Equal("Use an Orb of Transmutation", ControllerGlyphs.Render(named), "no stray brackets where the art was");
            Equal(false, ControllerGlyphs.HasUnnamedItemArt("Identify <<itemvisual:Unknown>> x 1"), "unknown art stays as the game strips it");
            ItemNames.Set(new());
        }

        private static void WrappedLabelsJoin()
        {
            Equal("Ocultar las pestañas de Solo retirar", Announcements.Flatten("Ocultar las pestañas de \nSolo retirar"), "link word");
            Equal("Socket a Support Gem of any colour", Announcements.Flatten("Socket a Support Gem of any\ncolour"), "lower case");
            Equal("1 Strength = +2 Life. 5 Strength = +1 Damage", Announcements.Flatten("1 Strength = +2 Life\n5 Strength = +1 Damage"), "two lines");
        }

        private static void ScrolledRowsAreNotNews()
        {
            var differ = Settled(out _);
            var scrolled = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch")));
            differ.Apply(scrolled);
            var changes = differ.Apply(scrolled);
            Equal(true, changes.Single().Scrolled, "a row came in as another left");

            var grown = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch"), Text(13, "Details")));
            differ.Apply(grown);
            differ.Apply(grown);
            differ.Apply(grown);
            var details = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch"), Text(13, "Details"), Text(14, "More")));
            differ.Apply(details);
            Equal(false, differ.Apply(details).Single().Scrolled, "a text added with nothing gone");
        }

        private const string TierLabel = "tier_label";

        private static void FocusDetailsAreNotScrolledRows()
        {
            Equal(true, FocusDetails.HeadedBy(new[] { Text(1, "Destrozahuesos"), Text(2, "AdE, Cuerpo a cuerpo, Golpe") }, "Destrozahuesos, Nivel 1", TierLabel),
                "a skill's details under its name, as the old ones left");
            Equal(false, FocusDetails.HeadedBy(new[] { Text(1, "Golpe perfecto"), Text(2, "Nivel 5") }, "Golpe perfecto, Nivel 5", TierLabel), "the focused row scrolling in");
            Equal(false, FocusDetails.HeadedBy(new[] { Text(1, "Acercar la cámara"), Text(2, "Activado") }, "Restricciones de equipamiento", TierLabel), "other rows scrolling in");
        }

        private static void KeptLessonIsHeardWhole()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 26, 10, 33, 21);
            Equal(true, floor.Admit("Orbe de aumento, 2, columna 1, fila 1", true, start), "the focus goes out");
            Equal(true, floor.Keep("Navega a tu arbol de pasivas", true, 2000, start.AddMilliseconds(200)), "the lesson in a menu cuts in");
            Equal(false, floor.Admit("Lanza de madera dura", true, start.AddMilliseconds(500)), "a move waits");
            Equal(false, floor.Admit("Comparar, clic del stick derecho", false, start.AddMilliseconds(600)), "its legend waits after it");
            Equal(false, floor.Admit("Vacío, columna 4, fila 2", true, start.AddMilliseconds(900)), "a later move replaces what waited");
            Equal(false, floor.Keep("Objeto sin identificar", true, 2000, start.AddMilliseconds(1000)), "a lesson never cuts the one being said");
            Equal(0, floor.Release(start.AddMilliseconds(1500)).Count, "nothing while the lesson is said");
            Equal("Vacío, columna 4, fila 2", string.Join("|", floor.Release(start.AddMilliseconds(4300))), "the latest move after the lessons");
            Equal(true, floor.Admit("Vacío, columna 3, fila 2", true, start.AddMilliseconds(4400)), "the floor is free again");
            floor.Keep("Navega a tu arbol de pasivas", true, 2000, start.AddMilliseconds(5000));
            floor.Admit("Inventario", true, start.AddMilliseconds(5100));
            floor.Clear(start.AddMilliseconds(5200));
            Equal(0, floor.Release(start.AddMilliseconds(9000)).Count, "stopped speech drops what waited");
        }

        private static void KeptLessonHoldsFromItsStart()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 26, 10, 32, 42);
            floor.Sent(new string('x', 100), true, start);
            Equal(false, floor.Keep("¡Has recibido un punto de pasivas!", false, 2000, start), "in play the lesson waits its turn");
            Equal(true, floor.Held(start.AddMilliseconds(100 * SpeechFloor.SpokenMsPerChar + 1900)), "held until said after what was ahead");
            Equal(false, floor.Held(start.AddMilliseconds(100 * SpeechFloor.SpokenMsPerChar + 2100)), "then free");

            var later = start.AddMinutes(1);
            floor.Sent(new string('x', 1000), true, later);
            floor.Keep("lesson", false, 2000, later);
            Equal(false, floor.Held(later.AddMilliseconds(SpeechFloor.MaxAheadMs + 2100)), "a long line ahead delays the hold only so far");
        }

        private static void PlayerMoveCutsAMessage()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 28, 12, 35, 9);
            floor.Keep("Vendido: Lanza con punta de hierro, raro, por 124 de oro", true, 2500, start, yieldsToMoves: true);
            Equal(false, floor.Admit("Vacío, columna 2, fila 1", true, start.AddMilliseconds(100)), "the game's refresh waits for the sale");
            // Queued behind the sale, the move left the player waiting to hear where they were (user 2026-11-11).
            Equal(true, floor.Move("Orbe de aumento, columna 3, fila 1", true, start.AddMilliseconds(230)), "the player's move cuts the sale");
            Equal(false, floor.Held(start.AddMilliseconds(240)), "and the sale holds nothing more");
            Equal(0, floor.Release(start.AddMilliseconds(3000)).Count, "the stale refresh is dropped");
            Equal(true, floor.Move("Pergamino de sabiduría, 5, columna 1, fila 1", true, start.AddMilliseconds(600)), "the next move");

            var lesson = start.AddMinutes(1);
            floor.Keep("Navega a tu arbol de pasivas", true, 2000, lesson);
            Equal(false, floor.Move("Vacío, columna 4, fila 2", true, lesson.AddMilliseconds(300)), "a lesson still holds the move");

            // The quest page on entering the camp held Renly's options back for 12 s (2026-11-11).
            var page = start.AddMinutes(2);
            floor.Keep("Misión: Secretos en la oscuridad. Atraviesa Sierraclara y adéntrate en el Bosque singular", false, 6000, page, yieldsToMoves: true);
            Equal(false, floor.Admit("Renly, A", true, page.AddMilliseconds(500)), "what the game shows meanwhile waits for the page");
            Equal(true, floor.Move("El devorador, 6 de 8", true, page.AddMilliseconds(900)), "the player's move cuts the page");
            Equal(false, floor.Held(page.AddMilliseconds(1000)), "and the page holds nothing more");

            // An area's line and its quest page go out one behind the other; the scanner's answer waited for both (2026-11-11).
            var arrival = start.AddMinutes(3);
            floor.Keep("Área: Mausoleo del pretor, nivel 8. Esquina inferior derecha.", false, 2500, arrival, yieldsToMoves: true);
            floor.Keep("Misión: Lamentos entre lápidas. Registra el Mausoleo en busca de Draven y su consorte", false, 3800, arrival.AddMilliseconds(20), yieldsToMoves: true);
            Equal(false, floor.Admit("Cementerio de los eternos, A", true, arrival.AddMilliseconds(200)), "the exit's prompt waits for both");
            Equal(true, floor.Move("Salidas. Cementerio de los eternos, 3 pasos", true, arrival.AddMilliseconds(3000)), "the scanner's answer cuts both");
            Equal(0, floor.Release(arrival.AddMilliseconds(3100)).Count, "and the prompt that was to cut in is stale");

            // What happened behind a page is not lost to the move that cuts the page: it follows the move.
            var pickup = start.AddMinutes(4);
            floor.Keep("Misión: Lamentos entre lápidas. Registra el Mausoleo en busca de Draven y su consorte", false, 3800, pickup, yieldsToMoves: true);
            Equal(false, floor.Admit("Recogido 5 de oro", false, pickup.AddMilliseconds(300)), "a pick-up waits for the page");
            Equal(false, floor.Admit("Vida 50 por ciento", false, pickup.AddMilliseconds(350), reading: true), "and so does a reading");
            Equal(true, floor.Move("Tiesto, A", true, pickup.AddMilliseconds(900)), "what A uses cuts the page");
            Equal("Recogido 5 de oro|Vida 50 por ciento", string.Join("|", floor.Release(pickup.AddMilliseconds(950))), "both follow the move, the reading still fresh");
        }

        private static void ANoticesCopyInTheHintBoxIsNoLesson()
        {
            // The hint box showed "+20 a la vida máxima" as the chat said who received it: kept as a lesson, it held the cursor for 3 s (2026-11-11).
            const string notice = "yarkidius ha recibido +20 a la vida máxima.";
            const string copy = "+20 a la vida máxima";
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 11, 11, 18, 49, 32, DateTimeKind.Utc);
            floor.Keep(copy, false, 1500, start);
            Equal(false, floor.Move("Vacío, columna 5, fila 5", true, start.AddMilliseconds(30)), "a lesson holds the move");
            floor.Keep(notice, true, SpeechFloor.MessageHoldMs(notice), start.AddMilliseconds(80), yieldsToMoves: true);
            Equal(true, floor.Move("Arco de protector, columna 9, fila 1", true, start.AddMilliseconds(900)), "the copy of a notice does not");
            var lesson = start.AddMinutes(1);
            floor.Keep("Navega a tu arbol de pasivas", true, 2000, lesson);
            floor.Keep(notice, false, 1900, lesson.AddMilliseconds(80), yieldsToMoves: true);
            Equal(false, floor.Move("Vacío, columna 4, fila 2", true, lesson.AddMilliseconds(300)), "another notice leaves a lesson its hold");
        }

        private static void AMessageCuttingAMoveIsFollowedByIt()
        {
            // "Orbe regio…" 116 ms before "Movido a Alijo: Runa glacial menor": the cursor's place was never said (2026-11-11).
            const string place = "Orbe regio, columna 3, fila 3";
            const string moved = "Movido a Alijo: Runa glacial menor";
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 11, 11, 14, 49, 22, DateTimeKind.Utc);
            floor.Move(place, true, start);
            floor.Admit("Comprobar precio, mantener Triángulo", false, start.AddMilliseconds(50));
            Equal(true, floor.Keep(moved, true, SpeechFloor.MessageHoldMs(moved), start.AddMilliseconds(116), yieldsToMoves: true), "the message cuts the move");
            Equal(place, floor.MoveToResume, "which follows it");
            floor.Keep("Inventario lleno", false, 700, start.AddMilliseconds(300), yieldsToMoves: true);
            Equal(null, floor.MoveToResume, "a message queued behind cuts nothing");

            var heard = start.AddMinutes(1);
            floor.Move(place, true, heard);
            floor.Keep(moved, true, 1500, heard.AddMilliseconds(SpeechFloor.UnheardMs + 100), yieldsToMoves: true);
            Equal(null, floor.MoveToResume, "a place already heard is not said again");

            var refreshed = start.AddMinutes(2);
            floor.Move(place, true, refreshed);
            floor.Admit("Vacío, columna 3, fila 3", true, refreshed.AddMilliseconds(200));
            floor.Keep(moved, true, 1500, refreshed.AddMilliseconds(500), yieldsToMoves: true);
            Equal(null, floor.MoveToResume, "the game's refresh took the move's place");

            var page = start.AddMinutes(3);
            floor.Move("Urna, 12 pasos, izquierda", true, page);
            floor.Keep("Misión: Secretos en la oscuridad. Atraviesa Sierraclara y adéntrate en el Bosque singular", true, 6000, page.AddMilliseconds(200), yieldsToMoves: true);
            Equal(null, floor.MoveToResume, "a page is too long to wait behind");
        }

        private static void PendingLessonIsRemindedNowAndThen()
        {
            Equal(false, LessonReminder.IsDue(TimeSpan.FromSeconds(3), false), "the box flickering during play");
            Equal(true, LessonReminder.IsDue(TimeSpan.FromSeconds(6), false), "shown again during play: the game waits for it");
            Equal(false, LessonReminder.IsDue(TimeSpan.FromSeconds(30), true), "back with a panel soon after");
            Equal(true, LessonReminder.IsDue(TimeSpan.FromSeconds(61), true), "still pending a minute later");
        }
    }
}
