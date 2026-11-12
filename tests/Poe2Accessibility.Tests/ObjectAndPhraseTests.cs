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

    /// <summary>Object sounds, opening and switches, life warnings, phrase tables, captions, places and tooltips.</summary>
    public static class ObjectAndPhraseTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ObjectsPlayTheSoundTheGameGivesThem), ObjectsPlayTheSoundTheGameGivesThem),
            (nameof(OpeningSoundsComeFromTheOpeningAnimation), OpeningSoundsComeFromTheOpeningAnimation),
            (nameof(SwitchesAreToldFromBreakables), SwitchesAreToldFromBreakables),
            (nameof(TwoNpcsAPlayerAndABodyAreAllHeard), TwoNpcsAPlayerAndABodyAreAllHeard),
            (nameof(LifeWarningsSpeakOncePerFall), LifeWarningsSpeakOncePerFall),
            (nameof(PhrasesFollowTheGameLanguage), PhrasesFollowTheGameLanguage),
            (nameof(OtherLanguagesUsePhraseTables), OtherLanguagesUsePhraseTables),
            (nameof(ChatSpeakersGetASpace), ChatSpeakersGetASpace),
            (nameof(SmallCapsCaptionsGetACapital), SmallCapsCaptionsGetACapital),
            (nameof(QuestRewardIconsAreNamed), QuestRewardIconsAreNamed),
            (nameof(MapPiecesAndPropsArePlaced), MapPiecesAndPropsArePlaced),
            (nameof(PlacesAreSaidOnceAsTheyChange), PlacesAreSaidOnceAsTheyChange),
            (nameof(ATooltipLineSwappedInPlaceIsSaid), ATooltipLineSwappedInPlaceIsSaid),
            (nameof(OnlyASwappedLineCutsIn), OnlyASwappedLineCutsIn),
            (nameof(AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice), AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice),
            (nameof(ConfirmGlyphMarksTheEntryBesideIt), ConfirmGlyphMarksTheEntryBesideIt),
        };

        private static void ObjectsPlayTheSoundTheGameGivesThem()
        {
            Breakables.UseObjectSounds(new[] { "Metadata/Chests/Badlands/Effigy_01\tEffigies_Open_01", "Metadata/Chests/G2_1/SnakeLadyPotionChest\tCorpses_Open", "not a line" });
            Equal("Effigies_Open_01", Breakables.Material("Metadata/Chests/Badlands/Effigy_01"), "an object no word names");
            Equal("Effigies_Open_01", Breakables.Material("Metadata/Chests/Badlands/Effigy_01@73"), "an instance's level is not part of its name");
            Equal("Corpses_Open", Breakables.Material("Metadata/Chests/G2_1/SnakeLadyPotionChest"), "a potion is no pot");
            Equal("urn", Breakables.Material("Metadata/Chests/Urns/Urn1"), "a word keeps its tuned sound");
            Equal("", Breakables.Material("Metadata/Chests/Gold/GoldPileSmall"), "the game gives it none: the common sound");
            Breakables.UseObjectSounds(Array.Empty<string>());
        }

        private static void OpeningSoundsComeFromTheOpeningAnimation()
        {
            const string Lid = "Audio/Sound Effects/Chests/Keth/Lid";
            const string Sounds = "SoundEvents\n{\n animations = '[{\"name\": \"idle\", \"events\": [{\"time\": 0.0, \"filename\": \"Audio/Sound Effects/Hum\"}]},"
                + "{\"name\": \"open\", \"events\": [{\"time\": 0.6, \"filename\": \"Audio/Sound Effects/Chests/Keth/Impact\"}, {\"time\": 0.0, \"filename\": \"" + Lid + "\"}]}]'\n}\n";
            const string Particles = "ParticleEffects\n{\n animations = '[{\"name\": \"open\", \"events\": [{\"time\": 0.0, \"filename\": \"Metadata/fx/dust.pet\"}]}]'\n}\n";
            Equal(Lid, AoSoundEvents.OpeningEvent(Particles + Sounds), "the earliest sound of the open animation, past other blocks' lists");
            Equal(null, AoSoundEvents.OpeningEvent("SoundEvents\n{\n}\n" + Particles), "an empty block does not borrow the next block's list");
            Equal(null, AoSoundEvents.OpeningEvent("SkinMesh\n{\n}\n"), "no sounds");
            Equal(null, AoSoundEvents.OpeningEvent("SoundEvents\n{\n animations = '[{broken]'\n}\n"), "a broken list");

            var files = new Dictionary<string, string> { ["Metadata/Child.ao"] = "version 3\nextends \"Metadata/Base\"\n", ["Metadata/Base.ao"] = Sounds };
            Equal(Lid, AoSoundEvents.OpeningEventOf("Metadata/Child.ao", path => files.GetValueOrDefault(path)), "a parent's sounds");
            Equal(null, AoSoundEvents.OpeningEventOf("Metadata/None.ao", path => files.GetValueOrDefault(path)), "a missing file");

            Equal("event:/Sound Effects/Chests/Keth/LidSG", AoSoundEvents.EventPaths(Lid)[0], "the event name");
            Equal("event:/Sound Effects/Chests/Spawn.ogg", AoSoundEvents.EventPaths("Audio/Sound Effects/Chests/Spawn.ogg%70")[1], "an older name, its volume dropped");
            Equal("Chests_4_0_G2_5_1_Effigies_Open_01", AoSoundEvents.FileName("Audio/Sound Effects/Chests/4.0/G2_5_1/Effigies/Open_01"), "a file name");
        }

        private static void SwitchesAreToldFromBreakables()
        {
            Equal(true, Switches.IsSwitch("Metadata/Terrain/Gallows/Act1/1_8/Objects/MausoleumSecretDoorSwitch"), "the mausoleum's switch");
            Equal(true, Switches.IsSwitch("Metadata/Terrain/Gallows/Act4/4_5_1/Objects/PrisonLever"), "a lever");
            Equal(false, Switches.IsSwitch("Metadata/Chests/Urns/Urn1"), "an urn");
            Equal("boulder", Breakables.Material("Metadata/Chests/Boulders/BoulderDirty1"), "a rock");
            Equal("rubble", Breakables.Material("Metadata/Chests/GoblinChests/GobRockPile01"), "a rock pile before a rock");
            Equal("bigpot", Breakables.Material("Metadata/Chests/TitanValleyPots/Pot01"), "a big pot before a pot");
            Equal("urn", Breakables.Material("Metadata/Chests/GraveyardDungeonChests/CryptUrn01"), "a crypt urn");
            Equal("egg", Breakables.Material("Metadata/Chests/BirdEggChests/BirdEggChest01"), "an egg");
            Equal("", Breakables.Material("Metadata/Chests/Village/Scarecrows/Scarecrow01"), "no material: the common sound");
            Equal(true, Breakables.IsCorpse("Metadata/Chests/CorpseChests_95702/PirateCorpse/PirateCorpseChest01"), "a corpse chest is a body");
            Equal(true, Breakables.IsCorpse("Metadata/Chests/Village/Impale01"), "the village's impaled victim is a body");
            Equal(false, Breakables.IsCorpse("Metadata/Chests/Village/Scarecrows/Scarecrow01"), "a scarecrow is not");
            Equal(false, Switches.IsSwitch("Metadata/Chests/LeverRoom/Chest1"), "only the object's own name counts");
        }

        private static void TwoNpcsAPlayerAndABodyAreAllHeard()
        {
            var scheduler = new EnemyCueScheduler(2, 100, 150, 600);
            var targets = new[]
            {
                new CueTarget(1, CueSynth.Layer.Friend, 0.2f, 0f, 0.8f),
                new CueTarget(2, CueSynth.Layer.Friend, -0.2f, 0f, 0.7f),
                new CueTarget(3, CueSynth.Layer.Friend, 0.5f, 0f, 0.2f),
                new CueTarget(4, CueSynth.Layer.Player, 0.4f, 0f, 0.5f),
                new CueTarget(5, CueSynth.Layer.PlayerDown, 1f, 0f, 0f),
                new CueTarget(6, CueSynth.Layer.None, 0f, 0f, 0.9f),
            };
            var played = new HashSet<uint>();
            for (var now = 0L; now < 2000; now += 10)
            {
                if (scheduler.Next(now, targets) is { } target)
                {
                    played.Add(target.Id);
                }
            }

            Equal("1 2 4 5 6", string.Join(" ", played.Order()), "the two nearest NPCs, the player, the body and the enemy; not the third NPC");
            var taps = CueSynth.Enemy(CueSynth.Layer.Player, 44100);
            var call = CueSynth.Enemy(CueSynth.Layer.PlayerDown, 44100);
            Equal(true, taps.Length > CueSynth.Tap(44100).Length && taps.All(sample => MathF.Abs(sample) <= 1f), "the player's double tap is longer than the NPC's, in range");
            Equal(true, call.Max(MathF.Abs) < taps.Max(MathF.Abs) && call.All(sample => MathF.Abs(sample) <= 1f), "the body's call is softer than the taps, in range");
        }

        private static void LifeWarningsSpeakOncePerFall()
        {
            var alarm = new VitalAlarm(10, 50, 25);
            string Feed(params int[] values) => string.Join(" ", values.Select(value => alarm.Update(value)?.ToString() ?? "-"));
            Equal("- 50 - - 25", Feed(100, 49, 45, 51, 20), "each threshold once on the way down");
            Equal("- 50", Feed(61, 40), "re-armed 10 points above");
            Equal("- 25", Feed(100, 10), "a big hit says only the lowest");
        }

        private static void PhrasesFollowTheGameLanguage()
        {
            try
            {
                Localization.Detect("en");
                Equal("2 of 5", Announcements.ListPosition(2, 5), "english position");
                Equal("Life 25 percent", Announcements.LowLife(25), "english life");
                Equal("Wounded Man, 9 steps, up left, no route", Announcements.ScanEntry("Wounded Man", 9, Announcements.StickDirections[3], 0, null, false), "english scan entry");
                Equal("right", Announcements.StickDirections[0], "english direction");
                Equal("D-pad right", Announcements.ButtonName("DPadRight", false), "english button");

                Localization.Detect("es");
                Equal("2 de 5", Announcements.ListPosition(2, 5), "spanish position");
                Equal("Vida 25 por ciento", Announcements.LowLife(25), "spanish life");
                Equal("Wounded Man, 9 pasos, arriba a la izquierda, sin ruta", Announcements.ScanEntry("Wounded Man", 9, Announcements.StickDirections[3], 0, null, false), "spanish scan entry");
                Equal("Renly, 1 paso, abajo 3, luego izquierda", Announcements.ScanEntry("Renly", 1, "abajo", 3, "izquierda", true), "spanish scan entry with a turn");
                Equal("cruceta derecha", Announcements.ButtonName("DPadRight", false), "spanish button");
                Equal("A", Announcements.ButtonName("Button4", false), "letters stay");
                Equal("Área: Sierraclara, nivel 2.", Announcements.Area("Sierraclara", 2), "area with its level");
                Equal("Área: Sierraclara.", Announcements.Area("Sierraclara"), "area level unknown");
                Equal("Nivel 4, nivel del área 2", Announcements.Levels(4, 2), "levels in the vitals");
                Equal("O GROSSO J, Guerrero, nivel 20", Announcements.OtherPlayer("O_GROSSO_J", "Guerrero", 20), "another player with level");

                Equal("es", Localization.ParseGameLanguage("[GENERAL]\nlanguage=en\n[LANGUAGE]\nlanguage=ES\n"), "ini section");
                Localization.Detect("auto");
                Equal(true, Localization.Languages.Contains(Localization.Language), "auto never throws");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void OtherLanguagesUsePhraseTables()
        {
            try
            {
                Equal("es", Localization.Normalize("ES"), "upper case code");
                Equal("pt", Localization.Normalize("pt-BR"), "regional code");
                Equal("zh-TW", Localization.Normalize("zh_tw"), "underscore code");
                Equal(null, Localization.Normalize("auto"), "auto is no language");

                Localization.Set("fr", new()
                {
                    ["Life {0} percent"] = "Vie à {0} pour cent",
                    ["left"] = "gauche",
                    ["{0} steps"] = "{0} pas",
                    ["{0}, {1}, {2}{3}"] = "{0} : {1}, {2}{3}",
                    [", no route"] = ", pas de chemin",
                    ["{0} of {1}"] = "{1} / {0} {2}",
                });
                Equal("Vie à 25 pour cent", Announcements.LowLife(25), "interpolated phrase by its format");
                Equal("gauche", Announcements.StickDirections[4], "plain phrase");
                Equal("up", Announcements.StickDirections[2], "missing phrase stays English");
                Equal("Una : 9 pas, gauche, pas de chemin", Announcements.ScanEntry("Una", 9, Announcements.StickDirections[4], 0, null, false), "nested phrases");
                Equal("2 of 5", Announcements.ListPosition(2, 5), "a broken translation falls back to English");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void ChatSpeakersGetASpace()
        {
            Equal("Renly: Esto es lo que tengo.", ChatDiff.Spoken("Renly:Esto es lo que tengo."), "speaker glued to the line");
            Equal("Una: Hola", ChatDiff.Spoken("Una: Hola"), "already spaced");
            Equal("Son las 12:30", ChatDiff.Spoken("Son las 12:30"), "a time keeps its colon");
            Equal("zzChopanz: Gran yelmo imperial", ChatDiff.Spoken("#zzChopanz:Gran yelmo imperial"), "global sign dropped");
            Equal("Ana: WTS", ChatDiff.Spoken("$Ana: WTS"), "trade sign dropped");
            var mine = new[] { "yarkey", string.Empty };
            Equal(true, ChatDiff.IsFrom("$yarkey: WTS botas", mine), "own trade line, said whatever the channel setting");
            Equal(true, ChatDiff.IsFrom("#<KAOS> yarkey:hola", mine), "own global line after a guild tag");
            Equal(true, ChatDiff.IsFrom("yarkey: ¡Necesito más tiempo!", mine), "own local remark");
            Equal(false, ChatDiff.IsFrom("#yarkeyz: hola", mine), "another player whose name starts the same");
            Equal(false, ChatDiff.IsFrom("@Para yarkey: hola", mine), "a whisper names its recipient, not the writer");
            Equal(false, ChatDiff.IsFrom("Son las 12:30", mine), "a line without a speaker");
            Equal(true, ChatDiff.IsPlayerChannel("#Ana: hola") && !ChatDiff.IsPlayerChannel("@De Ana: hola"), "whispers are not player channels");
            Equal("De Dahnte: hola", ChatDiff.Spoken("@De Dahnte:hola"), "whisper sign dropped");
            Equal("Ana: vamos", ChatDiff.Spoken("%Ana:vamos"), "party sign dropped");
        }

        private static void SmallCapsCaptionsGetACapital()
        {
            Equal("Reanudar juego", Announcements.Capitalised("reanudar juego"), "game menu entry");
            Equal("Confirmar A", Announcements.Capitalised("confirmar A"), "a button letter is not a capital word");
            Equal("kehgan, Nivel 2 Monje", Announcements.Capitalised("kehgan, Nivel 2 Monje"), "capitals in its words: as written");
            Equal("+ 0.0", Announcements.Capitalised("+ 0.0"), "no letter to raise");
            Equal(string.Empty, Announcements.Capitalised(string.Empty), "empty");
        }

        private static void QuestRewardIconsAreNamed()
        {
            Localization.Detect("es");
            Equal("gema de asistencia", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardSupportGem.dds|0,0,0,0|0"), "support gem");
            Equal("oro", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardGold.dds|0,0,0,0|0"), "gold");
            Equal("NewKind", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardNewKind.dds|0,0,0,0|0"), "a kind not known yet keeps its art name");
            Equal(null, QuestRewards.FromTextureKey("Art/2DItems/Currency/Ruthless/CoinPileTier1.dds|0,0,0,0|0"), "another image");
            Localization.Detect("en");
        }

        private static void MapPiecesAndPropsArePlaced()
        {
            const int sub = 23;
            var catalog = PlaceCatalog.Parse(@"{
                ""tiles"": { ""metadata/terrain/a/bridge_01.tdt"": ""bridge"", ""metadata/terrain/a/town.tdt"": ""mill"" },
                ""tileProps"": { ""metadata/terrain/a/bridge_01.tdt"": [[30, 5, ""cart""]] },
                ""rooms"": [ { ""arm"": ""town.arm"", ""anchors"": [[282, 252, ""waypoint""], [349, 237, ""well""]], ""props"": [[376, 211, ""cart""]] } ]
            }");
            var placements = new List<TilePlacement>
            {
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 0, new(0, 0)),
                new("metadata/terrain/a/bridge_01.tdt", 1, 0, 0, new(sub, 0)),
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 3, new(5 * sub, 2 * sub)),
                new("metadata/terrain/a/bridge_01.tdt", 1, 0, 3, new(6 * sub, 2 * sub)),
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 2, new(9 * sub, 2 * sub)),
            };
            placements.AddRange(Enumerable.Range(0, 10).Select(i => new TilePlacement("metadata/terrain/a/town.tdt", i, 0, 0, new(i * sub, 10 * sub))));
            placements.AddRange(Enumerable.Range(0, 20).Select(i => new TilePlacement("metadata/terrain/a/grass.tdt", 0, 0, 0, new(i * sub, 20 * sub))));
            placements.AddRange(Enumerable.Range(0, 40).Select(i => new TilePlacement("metadata/terrain/blank.tdt", 0, 0, 0, new(i * sub, 30 * sub))));
            var map = PlaceMap.Build(catalog, placements, sub);
            Equal("bridge", map.KindAt(new Vector2(30, 10)), "a named tile's sub-tiles");
            Equal(null, map.KindAt(new Vector2(30, 10 * sub + 5)), "a kind covering most of the area is the area itself");
            Equal(3, map.Pieces.Count(piece => piece.Kind == "bridge"), "separate bridges");
            Equal($"30,5 {5 * sub + 5},{2 * sub + sub - 30}", string.Join(" ", map.Props.Select(prop => $"{prop.Grid.X},{prop.Grid.Y}")), "props turned with their tile; an unknown turn leaves them out");
            Equal("2", string.Join(",", map.UnknownRotations), "and says which turn it was");
            Equal(false, map.MatchRooms(new[] { ("waypoint", new Vector2(282, 252)) }), "one anchor of two does not place a room");
            Equal(true, map.MatchRooms(new[] { ("waypoint", new Vector2(283, 252)), ("well", new Vector2(349, 236)) }), "both anchors place it");
            Equal(3, map.Props.Count, "the room's cart joins the tiles'");
        }

        private static void PlacesAreSaidOnceAsTheyChange()
        {
            var tracker = new PlaceTracker(23, 25, 60);
            var props = new List<(Vector2 Grid, string Kind)> { (new Vector2(100, 0), "cart") };
            string Walk(string? kind, float x) => string.Join("+", tracker.Update(kind, new Vector2(x, 0), props));
            Equal("", Walk("bridge", 0), "a piece waits one update");
            Equal("bridge", Walk("bridge", 2), "then it is said");
            Equal("", Walk("bridge", 4), "once");
            Equal("", Walk(null, 20), "plain ground near it keeps the name");
            Equal("", Walk("bridge", 24), "so the bridge's next piece is not news");
            Equal("cart", Walk(null, 80), "a prop in reach");
            Equal("", Walk(null, 90), "said once");
            Equal("", Walk(null, 200), "far away: forgotten");
            Equal("cart", Walk(null, 110), "and said again on the way back");
            Equal("", Walk("stairs", 300), "a new kind waits");
            Equal("stairs", Walk("stairs", 302), "and is said");
        }

        private static void ATooltipLineSwappedInPlaceIsSaid()
        {
            var bear = new[] { "Talismán", "Forma de oso", "Equipado: Set de armas" };
            var wolf = new[] { "Talismán", "Forma de hombre lobo", "Equipado: Set de armas" };
            const string focused = "Talismán cambiaformas, Talismán. Forma de oso. Equipado: Set de armas";
            string New(IReadOnlyList<string> said, IReadOnlyList<string> tooltip, string speech) =>
                string.Join(" | ", FocusDetails.NewTooltipLines(said, tooltip, speech));

            Equal("Forma de hombre lobo", New(bear, wolf, focused), "the form changed");
            Equal("Forma de oso", New(wolf, bear, focused), "and changed back, though the focus said it");
            Equal(string.Empty, New(bear, bear, focused), "nothing changed");
            Equal(string.Empty, New(Array.Empty<string>(), bear, focused), "built after the focus: all of it was said");
            Equal("Daño", New(new[] { "Talismán" }, new[] { "Talismán", "Daño" }, "Talismán"), "still building: only what the focus did not hold");
        }

        private static void OnlyASwappedLineCutsIn()
        {
            var bear = new[] { "Talismán", "Forma de oso" };
            Equal(true, FocusDetails.LineSwapped(bear, new[] { "Talismán", "Forma de hombre lobo" }), "another form");
            Equal(false, FocusDetails.LineSwapped(new[] { "Frasco", "Actualmente tiene 6 cargas" }, new[] { "Frasco", "Actualmente tiene 7 cargas" }), "a number ticking");
            Equal(false, FocusDetails.LineSwapped(new[] { "Talismán" }, bear), "still being built");
            Equal(false, FocusDetails.LineSwapped(bear, bear), "nothing changed");
        }

        private static void AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice()
        {
            const string notice = "malgdhler ha recibido +10% a la resistencia al hielo.";
            const string copy = "+10% a la resistencia al hielo";
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 11, 6, 23, 5, 4, DateTimeKind.Utc);
            floor.Sent(new string('x', 200), true, start); // an item's long text, 9 s
            var hold = SpeechFloor.MessageHoldMs(notice);
            Equal(true, floor.Keep(notice, true, hold, start.AddMilliseconds(400), yieldsToMoves: true), "the notice cuts the long text");
            Equal(true, floor.Echoes(copy, start.AddMilliseconds(600)), "the copy on screen is no news");
            Equal(false, floor.Echoes("Inventario lleno", start.AddMilliseconds(600)), "another message is");
            Equal(true, floor.Move("Grebas rústicas", true, start.AddMilliseconds(900)), "the cursor goes out at once");
            Equal(true, floor.Echoes(copy, start.AddMilliseconds(1000)), "a move does not bring the copy back");
            Equal(false, floor.Echoes(copy, start.AddMilliseconds(400 + hold + 1)), "said again once it is over");
        }

        private static void ConfirmGlyphMarksTheEntryBesideIt()
        {
            // Three dialogue options stacked 30 px apart, 200 px wide.
            var entries = new List<(System.Numerics.Vector2 Position, System.Numerics.Vector2 Size)>
            {
                (new(100, 100), new(200, 30)),
                (new(100, 130), new(200, 30)),
                (new(100, 160), new(200, 30)),
            };
            Equal(1, UiSemantics.NearestRect(entries, new(150, 145)), "glyph inside the second entry");
            Equal(2, UiSemantics.NearestRect(entries, new(80, 172)), "glyph drawn left of the third entry");
            Equal(0, UiSemantics.NearestRect(entries, new(80, 90)), "glyph above the list");
            Equal(-1, UiSemantics.NearestRect(new List<(System.Numerics.Vector2, System.Numerics.Vector2)>(), new(0, 0)), "no entries");
        }
    }
}
