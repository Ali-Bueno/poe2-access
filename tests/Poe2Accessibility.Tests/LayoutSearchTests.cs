namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.Utils;
    using Poe2Accessibility.Services;

    /// <summary>The host's runtime layout search against a fake game memory: a patch moves the fields, the search must follow.</summary>
    public static class LayoutSearchTests
    {
        private const long Heap = 0x10_0000_0000;
        private const long Player = Heap + 0x1000;
        private const long Monster = Heap + 0x2000;
        private const long Sentinel = Heap + 0x3000;
        private const long Root = Heap + 0x4000;
        private const long Left = Heap + 0x5000;
        private const long ServerData = Heap + 0x6000;
        private const long SleepSentinel = Heap + 0x7000;
        private const long PlayerList = Heap + 0x8000;
        private const long ServerPlayer = Heap + 0x9000;
        private const long Tiles = Heap + 0xA000;
        private const long Walkable = Heap + 0xB000;
        private const int TilesX = 10;
        private const int TilesY = 20;
        private const int BytesPerRow = 115;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(FieldsWhereMeasuredStayThere), FieldsWhereMeasuredStayThere),
            (nameof(FieldsAreFollowedHoweverFarTheyMoved), FieldsAreFollowedHoweverFarTheyMoved),
            (nameof(AnEmptyAreaHasNoWorld), AnEmptyAreaHasNoWorld),
            (nameof(TheLevelSurvivesFlagsAndFullVectors), TheLevelSurvivesFlagsAndFullVectors),
            (nameof(TheTopHalfOfAPointerIsNoHash), TheTopHalfOfAPointerIsNoHash),
            (nameof(AFieldNotFoundKeepsTheLastOne), AFieldNotFoundKeepsTheLastOne),
            (nameof(TheEntityIdIsTheListsKey), TheEntityIdIsTheListsKey),
            (nameof(MovedBytesComeBackWhereTheStructWantsThem), MovedBytesComeBackWhereTheStructWantsThem),
            (nameof(SegmentsDropWhatContinuesTheLastShift), SegmentsDropWhatContinuesTheLastShift),
            (nameof(VitalsPointBackAtTheirComponent), VitalsPointBackAtTheirComponent),
            (nameof(MeasuredVitalsLandOnTheOnesFound), MeasuredVitalsLandOnTheOnesFound),
            (nameof(ATradeOfferIsSaidWhenItChanges), ATradeOfferIsSaidWhenItChanges),
            (nameof(AStackThatGrewInATradeIsNoDeparture), AStackThatGrewInATradeIsNoDeparture),
            (nameof(AReactionSaysFriendForThePlayerAndFoeForMonsters), AReactionSaysFriendForThePlayerAndFoeForMonsters),
            (nameof(GameIdsAreReadFromTheCatalog), GameIdsAreReadFromTheCatalog),
            (nameof(WhatAStateGrantsIsNotTheState), WhatAStateGrantsIsNotTheState),
            (nameof(ANewKeyIsNewsAndAReaderUpdateIsNot), ANewKeyIsNewsAndAReaderUpdateIsNot),
            (nameof(EveryHeavyStunIsAHeavyStun), EveryHeavyStunIsAHeavyStun),
            (nameof(TheAnimationIsTheFieldThatHasACopy), TheAnimationIsTheFieldThatHasACopy),
            (nameof(TheAnimationIsFoundWhereAPatchLeftIt), TheAnimationIsFoundWhereAPatchLeftIt),
            (nameof(ACopyAheadOfTheOtherForAMomentBreaksNothing), ACopyAheadOfTheOtherForAMomentBreaksNothing),
            (nameof(OneActionIsNotEnoughToName), OneActionIsNotEnoughToName),
            (nameof(AFlagStoredTwiceIsNoAnimation), AFlagStoredTwiceIsNoAnimation),
            (nameof(PointerHalvesAreNoAnimation), PointerHalvesAreNoAnimation),
            (nameof(ActorsWithNoCopyLeaveTheFieldToItsBehaviour), ActorsWithNoCopyLeaveTheFieldToItsBehaviour),
            (nameof(BuffListsEndInAnId), BuffListsEndInAnId),
            (nameof(TheCameraIsKeptTwice), TheCameraIsKeptTwice),
            (nameof(PositionsLieInsideTheMap), PositionsLieInsideTheMap),
        };

        private static void FieldsWhereMeasuredStayThere()
        {
            var fields = AreaFields.Measured;
            var found = new AreaLayoutSearch(World()).Find(Area(fields), fields);
            Equal(fields, found, "fields that did not move");
        }

        private static void FieldsAreFollowedHoweverFarTheyMoved()
        {
            var measured = AreaFields.Measured;
            foreach (var shift in new[] { 8, 0x28, -0x20, 0x480 })
            {
                var moved = measured with
                {
                    Level = measured.Level + shift,
                    Hash = measured.Hash + shift,
                    Environments = measured.Environments + shift,
                    ServerData = measured.ServerData + shift,
                    LocalPlayer = measured.LocalPlayer + shift,
                    Entities = measured.Entities + shift,
                    Terrain = measured.Terrain + shift,
                };
                Equal(moved, new AreaLayoutSearch(World()).Find(Area(moved), measured), $"fields moved by {shift:X}");
            }

            // 0.5.5f: the first fields moved 8 bytes, the entities and the terrain 0x28.
            var patch = measured with
            {
                Level = measured.Level + 8,
                Hash = measured.Hash + 8,
                Environments = measured.Environments + 8,
                ServerData = measured.ServerData + 8,
                LocalPlayer = measured.LocalPlayer + 8,
                Entities = measured.Entities + 0x28,
                Terrain = measured.Terrain + 0x28,
            };
            Equal(patch, new AreaLayoutSearch(World()).Find(Area(patch), measured), "fields moved apart");
        }

        private static void AnEmptyAreaHasNoWorld()
        {
            var found = new AreaLayoutSearch(World()).Find(new byte[0x2000], AreaFields.Measured);
            Equal(false, found.HasWorld, "a block of zeros");
            Equal(-1, found.LocalPlayer, "no player in zeros");
        }

        private static void TheLevelSurvivesFlagsAndFullVectors()
        {
            var fields = AreaFields.Measured;
            var block = Area(fields);
            block[fields.Level + 11] = 1; // a flag after the level, seen in a cave of 0.5.5f
            Put(block, fields.Level + 12, Heap, Heap + 8, Heap + 8); // the vector a town fills
            Equal(fields.Level, new AreaLayoutSearch(World()).Find(block, fields).Level, "the level beside a flag and a full vector");
        }

        private static void TheTopHalfOfAPointerIsNoHash()
        {
            var fields = AreaFields.Measured;
            var block = Area(fields);
            BitConverter.TryWriteBytes(block.AsSpan(fields.Hash), 0x3E8u);
            Equal(-1, new AreaLayoutSearch(World()).Find(block, fields).Hash, "0x3E8, the top of a pointer");
        }

        private static void AFieldNotFoundKeepsTheLastOne()
        {
            var known = AreaFields.Measured;
            var found = new AreaFields(-1, -1, -1, known.ServerData + 8, known.LocalPlayer + 8, known.Entities, known.Terrain);
            var kept = found.Over(known);
            Equal(known.Level, kept.Level, "a level not found");
            Equal(known.LocalPlayer + 8, kept.LocalPlayer, "a player found");
        }

        private static void TheEntityIdIsTheListsKey()
        {
            const int measured = 0x88;
            foreach (var moved in new[] { 0x88, 0x80, 0x90 })
            {
                var fields = AreaFields.Measured;
                var memory = World(moved);
                Equal(moved, new AreaLayoutSearch(memory).FindEntityId(Area(fields), fields.Entities, measured), $"id at {moved:X}");
            }
        }

        private static void MovedBytesComeBackWhereTheStructWantsThem()
        {
            var raw = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var target = new byte[16];
            var segments = new[] { new RemapSegment(4, 8), new RemapSegment(8, 0), new RemapSegment(12, 4) };
            StructRemapper.Apply(raw, target, segments);
            Equal("0 1 2 3 12 13 14 15 8 9 10 11 16 17 18 19", string.Join(' ', target), "fields read from where they moved");
            Equal(24, StructRemapper.ReadSize(16, segments), "the read reaches the farthest field");
        }

        private static void SegmentsDropWhatContinuesTheLastShift()
        {
            var segments = StructRemapper.Segments(new[] { new RemapSegment(0x20, 8), new RemapSegment(0x10, 0), new RemapSegment(0x30, 8), new RemapSegment(0x40, 16) });
            Equal("0x20 8; 0x40 16", string.Join("; ", segments.Select(segment => $"0x{segment.From:X} {segment.Shift}")), "only real changes of shift");
            Equal(0, StructRemapper.Segments(new[] { new RemapSegment(0x20, 0) }).Length, "nothing moved");
        }

        private static void VitalsPointBackAtTheirComponent()
        {
            const long life = 0x1234_5678;
            var block = new byte[0x200];
            foreach (var at in new[] { 0x40, 0xA0, 0x100 })
            {
                BitConverter.TryWriteBytes(block.AsSpan(at + 8), life);
                BitConverter.TryWriteBytes(block.AsSpan(at + 0x2C), 100);
                BitConverter.TryWriteBytes(block.AsSpan(at + 0x30), 60);
            }

            BitConverter.TryWriteBytes(block.AsSpan(0x160 + 8), life);
            BitConverter.TryWriteBytes(block.AsSpan(0x160 + 0x2C), 10);
            BitConverter.TryWriteBytes(block.AsSpan(0x160 + 0x30), 60); // more than its total: no vital
            Equal("40 A0 100", string.Join(' ', StructShapes.Vitals(block, life, 8, 0x2C, 0x30).Select(at => at.ToString("X"))), "the vitals, in order");
        }

        private static void MeasuredVitalsLandOnTheOnesFound()
        {
            // 0.5.5f: seven vitals, of which GameHelper reads five.
            int[] measured = { 0x1B0, 0x208, 0x248, 0x2E8, 0x338 };
            int[] found = { 0x1B0, 0x208, 0x248, 0x2A0, 0x2E8, 0x338, 0x3A0 };
            Equal("1B0 208 248 2E8 338", Hex(StructShapes.PlaceVitals(measured, found)), "a struct that did not move");
            Equal("1B8 210 250 2F0 340", Hex(StructShapes.PlaceVitals(measured, found.Select(at => at + 8).ToArray())), "all moved together");
            Equal("1A0 1F8 238 2D8 328", Hex(StructShapes.PlaceVitals(measured, found.Select(at => at - 0x10).ToArray())), "all moved back");
            Equal("1B0 208 250 2F0 340", Hex(StructShapes.PlaceVitals(measured, new[] { 0x1B0, 0x208, 0x250, 0x2F0, 0x340 })), "the later ones moved");
            Equal(string.Empty, Hex(StructShapes.PlaceVitals(measured, Array.Empty<int>())), "none");
        }

        private static void ATradeOfferIsSaidWhenItChanges()
        {
            string? said = null;
            var nothing = Array.Empty<string>();
            Equal(null, TradeOfferNews.Line("La oferta de Una", nothing, "nada", ref said), "a new empty window says nothing");
            Equal("La oferta de Una: Esquirla, 1", TradeOfferNews.Line("La oferta de Una", new[] { "Esquirla, 1" }, "nada", ref said), "an offer appears");
            Equal(null, TradeOfferNews.Line("La oferta de Una", new[] { "Esquirla, 1" }, "nada", ref said), "the same offer is said once");
            Equal("La oferta de Una: Esquirla, 1, Orbe", TradeOfferNews.Line("La oferta de Una", new[] { "Esquirla, 1", "Orbe" }, "nada", ref said), "it grows");
            Equal("La oferta de Una: nada", TradeOfferNews.Line("La oferta de Una", nothing, "nada", ref said), "it empties");
        }

        private static void AStackThatGrewInATradeIsNoDeparture()
        {
            var moves = ItemMoves.Read(
                new[] { (GameHelper.RemoteEnums.InventoryName.MainInventory1, "Esquirla de transmutación, 2") },
                new[] { (GameHelper.RemoteEnums.InventoryName.MainInventory1, "Frasco de vida"), (GameHelper.RemoteEnums.InventoryName.MainInventory1, "Esquirla de transmutación") },
                _ => null,
                true,
                0,
                string.Empty);
            Equal(1, moves.Count, "one thing really left");
            Equal("Frasco de vida", moves[0].Name, "the flask left, not the shards");
            Equal(ItemMoveKind.DroppedOrUsed, moves[0].Kind, "it left for nowhere tracked");
        }

        private static void ANewKeyIsNewsAndAReaderUpdateIsNot()
        {
            var known = new[] { "# header", "buffdefinitions\tfrozen", "buffdefinitions\told", "worldareas\tG1_1" };
            var fresh = new[] { "buffdefinitions\tfrozen", "buffdefinitions\tslimed", "worldareas\tG1_1", "shrines\tArc" };
            var news = GameNews.Compare(known, fresh);
            Equal(1, news.Count, "one table changed, and a table the old list lacked is not news");
            Equal("slimed", news[0].Added.Single(), "the new buff");
            Equal("old", news[0].Removed.Single(), "the one gone");
            Equal(0, GameNews.Compare(fresh, fresh).Count, "nothing changed");
            var report = GameNews.Report(news, (table, key) => key == "slimed" ? "Slowed" : null).ToList();
            Equal("buffdefinitions: 1 new, 1 gone", report[0], "the table's line");
            Equal("  + slimed (Slowed)", report[1], "what the mod makes of it");
            Equal("  - old", report[2], "what left");
        }

        private static void WhatAStateGrantsIsNotTheState()
        {
            foreach (var id in new[]
            {
                "life_regeneration_from_curse", "fire_resistance_from_ignite", "support_electrocute_lineage_explode_on_death",
                "stormfire_support_shock_tracker", "unravelling_ignite", "compound_ignite_spreading_placeholder",
                "frozen_claws_dash_delay", "blind_beast_enrage", "unique_boots_shocked_ground_while_moving",
            })
            {
                Equal(null, StatusAilments.FromBuffId(id), id);
            }

            Equal(StatusAilment.Ignited, StatusAilments.FromBuffId("additional_ignite"), "'You're on fire' in the game's description");
            Equal(StatusAilment.Ignited, StatusAilments.FromBuffId("herald_of_ash_burning"), "an enemy's Herald of Ash burning the player");
            Equal(StatusAilment.Cursed, StatusAilments.FromBuffId("curse_enfeeble"), "a curse");
        }

        private static void GameIdsAreReadFromTheCatalog()
        {
            var ids = GameIds.Parse(new[]
            {
                "# a header",
                "animation 0 Idle",
                "animation 2 MoltenSlam",
                "stat current_rage 6996",
                "stat broken",
                "animation x Nothing",
            });
            Equal(true, ids.HasAnimations, "the table is known");
            Equal("Idle", ids.Animation(0), "a row");
            Equal("MoltenSlam", ids.Animation(2), "a later row");
            Equal(null, ids.Animation(1), "a row the catalog lacks");
            Equal(null, ids.Animation(3), "beyond the table");
            Equal(null, ids.Animation(-1), "no row");
            Equal(6996, ids.Stat("current_rage", 6995), "the game's key over the compiled one");
            Equal(7, ids.Stat("level", 7), "a stat not listed keeps the compiled key");
            Equal(false, GameIds.Parse(Array.Empty<string>()).HasAnimations, "no catalog");
        }

        private static void EveryHeavyStunIsAHeavyStun()
        {
            Equal(EnemyState.HeavyStunned, EnemyStates.FromAnimation("HeavyStunForesight"), "a variant the old list lacked");
            Equal(EnemyState.HeavyStunned, EnemyStates.FromAnimation("HeavyStun"), "the plain one");
            Equal(null, EnemyStates.FromAnimation("Stun"), "a light stun is no heavy one");
        }

        private static void AReactionSaysFriendForThePlayerAndFoeForMonsters()
        {
            var player = new byte[0x10];
            player[4] = 0x81; // the friendly bit carries a flag above it
            player[6] = 1;
            var foe = new byte[0x10];
            foe[6] = 1;
            var minion = new byte[0x10];
            minion[4] = 1;
            var monsters = new[] { foe, foe, foe, foe, minion };
            Equal(true, StructShapes.IsReaction(player, monsters, 4), "friend to the player, foe to most");
            Equal(false, StructShapes.IsReaction(player, monsters, 6), "the same for both says nothing");
            Equal(false, StructShapes.IsReaction(player, new[] { minion, minion, minion, foe }, 4), "a crowd of minions");
            Equal(false, StructShapes.IsReaction(player, monsters, 0x20), "past the block");

            var twin = player.ToArray();
            twin[0xB] = 1;
            Equal(4, StructShapes.FindReaction(twin, monsters, 8, 8), "the first of two that read alike, whichever is nearer");
            Equal(-1, StructShapes.FindReaction(twin, monsters, 0x40, 8), "nothing around");
        }

        private const int ActorBlock = 0xA00;
        private const int AnimationAt = 0x8B0;
        private const int CopyAt = 0x380;

        /// <summary>An actor's reading: its table pointer, and the given fields.</summary>
        private static byte[] ActorReading(params (int Offset, int Value)[] fields)
        {
            var block = new byte[ActorBlock];
            BitConverter.TryWriteBytes(block.AsSpan(0), Heap);
            foreach (var (offset, value) in fields)
            {
                BitConverter.TryWriteBytes(block.AsSpan(offset), value);
            }

            return block;
        }

        /// <summary>Actors running and striking, the id in both copies; then at rest again.</summary>
        private static AnimationFieldSearch Acting(int copy, int field, int differEvery = 0)
        {
            var search = new AnimationFieldSearch();
            var readings = 0;
            for (var round = 0; round < 12; round++)
            {
                var id = round % 2 == 0 ? 4 : 195;
                for (var actor = 0; actor < 10; actor++)
                {
                    search.Add(actor, ActorReading());
                    for (var moment = 0; moment < 3; moment++)
                    {
                        var ahead = differEvery > 0 && ++readings % differEvery == 0;
                        search.Add(actor, ActorReading((copy, id), (field, ahead ? 0 : id)));
                    }
                }
            }

            return search;
        }

        private static void TheAnimationIsTheFieldThatHasACopy()
        {
            var search = Acting(CopyAt, AnimationAt);
            Equal(AnimationAt, search.Find(AnimationAt), "the copy nearest the measured place");
            Equal(false, search.Enough, "a moment of play proves nothing: only a pair ends the search this early");
        }

        private static void TheAnimationIsFoundWhereAPatchLeftIt() =>
            Equal(AnimationAt + 0x10, Acting(CopyAt + 8, AnimationAt + 0x10).Find(AnimationAt), "both copies moved");

        private static void ACopyAheadOfTheOtherForAMomentBreaksNothing() =>
            Equal(AnimationAt, Acting(CopyAt, AnimationAt, 100).Find(AnimationAt), "the game writes while it is read");

        private static void OneActionIsNotEnoughToName()
        {
            var search = new AnimationFieldSearch();
            for (var round = 0; round < 40; round++)
            {
                search.Add(1, ActorReading());
                search.Add(1, ActorReading((CopyAt, 195), (AnimationAt, 195)));
            }

            Equal(-1, search.Find(AnimationAt), "a run alone could be anything stored twice");
            Equal(false, search.Enough, "one actor, in town: nothing can be said to be lost");
        }

        private static void AFlagStoredTwiceIsNoAnimation()
        {
            var search = new AnimationFieldSearch();
            for (var round = 0; round < 40; round++)
            {
                for (var actor = 0; actor < 10; actor++)
                {
                    search.Add(actor, ActorReading());
                    search.Add(actor, ActorReading((0x100, 1), (0x600, 1)));
                }
            }

            Equal(-1, search.Find(AnimationAt), "one value only");
        }

        private static void PointerHalvesAreNoAnimation()
        {
            var search = new AnimationFieldSearch();
            for (var round = 0; round < 40; round++)
            {
                for (var actor = 0; actor < 10; actor++)
                {
                    var half = 972 + (round % 2);
                    search.Add(actor, ActorReading());
                    search.Add(actor, ActorReading((0x100, 0x12345678), (0x104, half), (0x600, 0x12345678), (0x604, half)));
                }
            }

            Equal(-1, search.Find(AnimationAt), "the upper half of a pointer, twice");
        }

        private static void ActorsWithNoCopyLeaveTheFieldToItsBehaviour()
        {
            var search = new AnimationFieldSearch();
            var rest = ActorReading();
            var run = ActorReading((AnimationAt, 4));
            var blow = ActorReading((AnimationAt, 195));
            for (var round = 0; round < 3000; round++)
            {
                for (var actor = 0; actor < 10; actor++)
                {
                    search.Add(actor, rest);
                    search.Add(actor, round % 2 == 0 ? run : blow);
                }
            }

            Equal(-1, search.Find(AnimationAt), "no pair");
            Equal(true, search.Enough, "plenty of actors moved");
            Equal(true, search.Behaves(AnimationAt), "it still rests at zero and takes several ids");
            Equal(false, search.Behaves(CopyAt), "this one never moved");
        }

        private static string Hex(IEnumerable<int> offsets) => string.Join(' ', offsets.Select(at => at.ToString("X")));

        private static void BuffListsEndInAnId()
        {
            var memory = World();
            var block = new byte[0x40];
            Put(block, 0x10, Heap + 0x100, Heap + 0x108, Heap + 0x110);
            memory.Pointers[Heap + 0x100] = Heap + 0x200;
            memory.Pointers[Heap + 0x208] = Heap + 0x300;
            memory.Pointers[Heap + 0x300] = Heap + 0x400;
            memory.Texts[Heap + 0x400] = "herald_of_thunder";
            Equal(true, StructShapes.IsBuffList(block, 0x10, memory, 8), "a chain that ends in an id");
            memory.Texts[Heap + 0x400] = "Not an Id";
            Equal(false, StructShapes.IsBuffList(block, 0x10, memory, 8), "a chain that ends in other text");
            Equal(false, StructShapes.IsBuffList(block, 0x18, memory, 8), "no vector");
        }

        private static void TheCameraIsKeptTwice()
        {
            var block = new byte[0x100];
            for (var cell = 0; cell < 16; cell++)
            {
                BitConverter.TryWriteBytes(block.AsSpan(0x20 + (cell * 4)), cell + 1f);
                BitConverter.TryWriteBytes(block.AsSpan(0x60 + (cell * 4)), cell + 1f);
            }

            Equal(true, StructShapes.IsCameraMatrix(block, 0x20), "a matrix and its copy");
            Equal(false, StructShapes.IsCameraMatrix(block, 0x24), "next to it");
            Equal(false, StructShapes.IsCameraMatrix(new byte[0x100], 0x20), "zeros equal zeros");
            BitConverter.TryWriteBytes(block.AsSpan(0x60), 99f);
            Equal(false, StructShapes.IsCameraMatrix(block, 0x20), "two different matrices");
        }

        private static void PositionsLieInsideTheMap()
        {
            var block = new byte[0x20];
            BitConverter.TryWriteBytes(block.AsSpan(4), 2500f);
            BitConverter.TryWriteBytes(block.AsSpan(8), 1200f);
            BitConverter.TryWriteBytes(block.AsSpan(12), -50f);
            Equal(true, StructShapes.IsWorldPosition(block, 4, 5000f, 5000f), "inside");
            Equal(false, StructShapes.IsWorldPosition(block, 4, 1000f, 5000f), "beyond the width");
            Equal(false, StructShapes.IsWorldPosition(block, 0, 5000f, 5000f), "a zero");
            BitConverter.TryWriteBytes(block.AsSpan(4), float.NaN);
            Equal(false, StructShapes.IsWorldPosition(block, 4, 5000f, 5000f), "not a number");
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
            }
        }

        private static void Put(byte[] block, int at, params long[] values)
        {
            foreach (var value in values)
            {
                BitConverter.TryWriteBytes(block.AsSpan(at), value);
                at += 8;
            }
        }

        /// <summary>An area instance with every field at <paramref name="at"/>, and noise a search must not take for them.</summary>
        private static byte[] Area(AreaFields at)
        {
            var block = new byte[0x2000];
            BitConverter.TryWriteBytes(block.AsSpan(at.Level), 18);
            BitConverter.TryWriteBytes(block.AsSpan(at.Hash), 0x2E570612u);
            Put(block, at.Environments, Heap + 0xC00, Heap + 0xC10, Heap + 0xC10);
            Put(block, at.ServerData, ServerData);
            Put(block, at.LocalPlayer, Player);

            // The same player again, as in the game, with no server data before it.
            Put(block, at.LocalPlayer + 0x600, Player);
            Put(block, at.Entities, Sentinel, 234, SleepSentinel, 0);
            Put(block, at.Terrain + 0x18, TilesX, TilesY);
            Put(block, at.Terrain + 0x28, Tiles, Tiles + (TilesX * TilesY * 0x38), Tiles + (TilesX * TilesY * 0x38));
            Put(block, at.Terrain + 0xD0, Walkable, Walkable + (BytesPerRow * TilesY * 23), Walkable + (BytesPerRow * TilesY * 23));
            BitConverter.TryWriteBytes(block.AsSpan(at.Terrain + 0x130), BytesPerRow);
            return block;
        }

        private static FakeMemory World(int entityId = 0x88)
        {
            var memory = new FakeMemory();
            memory.Paths[Player] = "Metadata/Characters/StrDex/StrDexFourb";
            memory.Paths[Monster] = "Metadata/Monsters/FungusZombie/FungusZombieFungalmancer";
            memory.Node(Sentinel, 0, Root, 0, true, 0, 0);
            memory.Node(Root, Left, Sentinel, 0, false, 0xC000050C, Monster);
            memory.Node(Left, 0, Root, 0, false, 0x987, Player);
            memory.Node(SleepSentinel, 0, SleepSentinel, 0, true, 0, 0);
            memory.Blocks[Monster] = Body(entityId, 0xC000050C);
            memory.Blocks[Player] = Body(entityId, 0x987);
            memory.Vector(ServerData + 0x48, PlayerList, 8);
            memory.Pointers[PlayerList] = ServerPlayer;
            memory.Vector(ServerPlayer + 0x320, Heap + 0xD000, 0x18 * 117);
            return memory;
        }

        private static byte[] Body(int idAt, uint id)
        {
            var body = new byte[0x100];
            BitConverter.TryWriteBytes(body.AsSpan(idAt), id);
            return body;
        }

        private sealed class FakeMemory : IAreaMemory
        {
            public Dictionary<long, string> Paths { get; } = new();

            public Dictionary<long, string> Texts { get; } = new();

            public Dictionary<long, byte[]> Blocks { get; } = new();

            public Dictionary<long, long> Pointers { get; } = new();

            public byte[] Read(long address, int size)
            {
                if (this.Blocks.TryGetValue(address, out var block))
                {
                    return block.Length >= size ? block[..size] : Array.Empty<byte>();
                }

                return size == 8 && this.Pointers.TryGetValue(address, out var pointer) ? BitConverter.GetBytes(pointer) : Array.Empty<byte>();
            }

            public string EntityPath(long entity) => this.Paths.GetValueOrDefault(entity, string.Empty);

            public string WideText(long address) => this.Texts.GetValueOrDefault(address, string.Empty);

            public void Node(long at, long left, long parent, long right, bool isNil, long key, long value)
            {
                var node = new byte[0x38];
                Put(node, 0, left, parent, right);
                node[0x19] = isNil ? (byte)1 : (byte)0;
                Put(node, 0x20, key, value);
                this.Blocks[at] = node;
            }

            public void Vector(long at, long first, long bytes)
            {
                var vector = new byte[0x18];
                Put(vector, 0, first, first + bytes, first + bytes);
                this.Blocks[at] = vector;
            }
        }
    }
}
