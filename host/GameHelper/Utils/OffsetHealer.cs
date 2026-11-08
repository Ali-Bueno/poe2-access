// <copyright file="OffsetHealer.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.Ui;
    using GameOffsets.Natives;
    using GameOffsets.Objects;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.FilesStructures;
    using GameOffsets.Objects.States;
    using GameOffsets.Objects.States.InGameState;
    using GameOffsets.Objects.UiElement;

    /// <summary>What a sweep of the game's structs found for one of them.</summary>
    public enum StructHealth
    {
        /// <summary>Its checked fields hold what they must.</summary>
        Intact,

        /// <summary>It was broken and now reads from where its fields moved.</summary>
        Moved,

        /// <summary>It is broken and no place fits.</summary>
        Lost,

        /// <summary>Nothing live to judge it on, or nothing in it can be checked.</summary>
        NotJudged,
    }

    /// <summary>One struct's outcome: its name, its health, where its first moved field went, and the reason.</summary>
    public readonly record struct StructOutcome(string Name, StructHealth Health, int From, int To, string Detail);

    /// <summary>
    ///     Finds where a patch left the game's structs, with no one at the keyboard: the fields the mod lives on by what they
    ///     must hold (<see cref="StructShapes"/>), the rest by GameHelper's offset verifier. A struct that holds what it must
    ///     is never touched; one that moved is read through <see cref="StructRemap{T}"/>.
    /// </summary>
    public static class OffsetHealer
    {
        /// <summary>The part that says where things stand.</summary>
        public const string PositionsPart = "positions";

        /// <summary>The part that says what actors are doing.</summary>
        public const string AnimationPart = "animations";

        /// <summary>The part that tells friend from foe.</summary>
        public const string ReactionPart = "reaction";

        /// <summary>The part that leads from an entity to its path (details pointer, path string).</summary>
        public const string EntityPathPart = "entity path";

        /// <summary>The part that holds an entity's components.</summary>
        public const string ComponentsPart = "entity components";

        /// <summary>The part that names an entity's components (lookup pointer, name map).</summary>
        public const string LookUpPart = "component lookup";

        /// <summary>The part that says which state is current (the game state object's stack).</summary>
        public const string StateStackPart = "game state stack";

        /// <summary>The part that lists the game's states.</summary>
        public const string StateListPart = "game state list";

        /// <summary>The part every walk of the UI tree needs (self, children, parent).</summary>
        public const string ElementTreePart = "element tree";

        /// <summary>The part that places UI elements on the screen (position, scale, size).</summary>
        public const string ElementRectsPart = "element rects";

        /// <summary>The part that says whether a UI element shows (flags, scale index).</summary>
        public const string ElementFlagsPart = "element flags";

        // Vitals that must point back before their inner layout is believed (the game has seven, GameHelper reads five).
        private const int MinVitals = 3;

        // Entities the sweep judges the entity layout on, besides the player.
        private const int EntitiesJudged = 4;

        private const int SearchRadius = 0x200;
        private const int GameStateBytes = 0x1000;
        private const int MinMonstersForReaction = 5;
        private const int ReactionRadius = 0x40;

        // The player stands near the screen's middle; a wrong position lands anywhere (0.8 of the half screen).
        private const float CentreShare = 0.8f;

        private const float WorldPerTile = 250f;

        // Judged here or by their own searches (AreaInstanceLayout, JudgeEntityLayout, JudgeUiElementBase), not by the verifier.
        private static readonly HashSet<Type> OwnJudgment = new()
        {
            typeof(AreaInstanceOffsets), typeof(EntityOffsets), typeof(UiElementBaseOffset), typeof(LifeOffset), typeof(BuffsOffsets), typeof(WorldItemOffsets), typeof(InventoryStruct),
            typeof(ActorOffset), typeof(RenderOffsets), typeof(StatsOffsets),
        };

        private static readonly Dictionary<Type, RemapSegment[]> Healed = new();

        /// <summary>Which segments the code placed, so a later build's reading replaces them and a live one is left alone.</summary>
        private static readonly Dictionary<Type, HashSet<int>> FromCode = new();

        /// <summary>Checks every struct with something live to judge it on; call on the render thread.</summary>
        public static IReadOnlyList<StructOutcome> Sweep()
        {
            var state = Core.States.InGameStateObject;
            var area = state.CurrentAreaInstance;
            var memory = new GameMemory(Core.Process.Handle);
            bool cameraKnown;
            lock (Healed)
            {
                cameraKnown = Healed.ContainsKey(typeof(WorldDataOffset));
            }

            var camera = JudgeCamera(state.CurrentWorldInstance.Address);
            var outcomes = new List<StructOutcome>
            {
                camera,
                JudgeLife(area.Player),
                JudgeBuffs(area.Player, memory),
                JudgeSkills(area.Player, memory),
                JudgeStats(area.Player, memory),
            };

            outcomes.Add(JudgeGroundItems(area, memory));
            outcomes.Add(JudgeInventories(area, memory));
            outcomes.AddRange(JudgeEntityLayout(memory, area.AwakeEntities.Values.Where(entity => entity.IsValid).Take(EntitiesJudged)
                .Select(entity => entity.Address.ToInt64()).Prepend(area.Player.Address.ToInt64()).ToList()));

            // The position is judged through the camera, which only reads from its new place on the next frame.
            outcomes.Add(camera.Health == StructHealth.Moved && !cameraKnown ? NotJudged(PositionsPart, "the camera just moved") : JudgePosition(area));
            foreach (var probe in OffsetHelperEngine.RunSweep().Probes)
            {
                if (probe.StructType is { } type && !OwnJudgment.Contains(type))
                {
                    outcomes.Add(Verified(probe, type));
                }
            }

            return outcomes;
        }

        /// <summary>Gets the offset of an entity's id this GameHelper was written for.</summary>
        internal static int MeasuredEntityId { get; } = OffsetOf<EntityOffsets>(nameof(EntityOffsets.Id));

        /// <summary>
        ///     Looks through the game state for the area instance when its pointer leads to no world; true once found.
        ///     Call on the render thread, in game.
        /// </summary>
        public static bool LocateArea()
        {
            var state = Core.States.InGameStateObject;
            var area = state.CurrentAreaInstance;
            var search = new AreaLayoutSearch(new GameMemory(Core.Process.Handle));
            bool HoldsWorld(long pointer)
            {
                var candidate = Block(new IntPtr(pointer), AreaInstanceLayout.BlockSize);
                return candidate.Length > 0 && search.Find(candidate, area.Layout.Fields).HasWorld;
            }

            return LocatePointer(state.Address, nameof(InGameStateOffset.AreaInstanceData), HoldsWorld);
        }

        /// <summary>Where <paramref name="offset"/> of a struct stands in this game build, for readers outside the struct.</summary>
        public static int Follow(Type structType, int offset)
        {
            lock (Healed)
            {
                if (!Healed.TryGetValue(structType, out var segments))
                {
                    return offset;
                }

                var last = segments.LastOrDefault(segment => segment.From <= offset);
                return offset + last.Shift;
            }
        }

        /// <summary>The entity id stands at <paramref name="found"/> (negative: unknown); what follows it moves along, what is before it stays.</summary>
        internal static void FollowEntityId(int found)
        {
            if (found >= 0)
            {
                Place(typeof(EntityOffsets), (MeasuredEntityId, found));
            }
        }

        /// <summary>
        ///     Judges where <paramref name="entities"/> keep their path and components (<see cref="EntityLayoutSearch"/>) and reads
        ///     every entity from there. Empty when none of them reads as an entity, which says nothing about the layout.
        /// </summary>
        internal static IReadOnlyList<StructOutcome> JudgeEntityLayout(IAreaMemory memory, IReadOnlyList<long> entities)
        {
            var measured = EntityLayout.Measured;
            var found = new EntityLayoutSearch(memory).Find(entities, measured);
            if (found.Details < 0 && found.Components < 0)
            {
                return Array.Empty<StructOutcome>();
            }

            Place(typeof(EntityOffsets), (measured.Details, found.Details), (measured.Components, found.Components), (MeasuredEntityId, Follow(typeof(EntityOffsets), MeasuredEntityId)));
            Place(typeof(EntityDetails), (measured.Path, found.Path), (measured.LookUp, found.LookUp));
            Place(typeof(ComponentLookUpStruct), (measured.NameMap, found.NameMap));
            return new[]
            {
                Outcome(EntityPathPart, (measured.Details, found.Details), (measured.Path, found.Path)),
                Outcome(ComponentsPart, (measured.Components, found.Components)),
                Outcome(LookUpPart, (measured.LookUp, found.LookUp), (measured.NameMap, found.NameMap)),
            };
        }

        /// <summary>
        ///     Repairs the entity layout from the area's own entities while the area shows no world; true when a field moved.
        ///     Nothing is searched while one of them reads its path as things stand.
        /// </summary>
        internal static bool RepairEntityLayout(IAreaMemory memory, IReadOnlyList<long> entities)
        {
            var measured = EntityLayout.Measured;
            var now = new EntityLayout(
                Follow(typeof(EntityOffsets), measured.Details),
                Follow(typeof(EntityDetails), measured.Path),
                Follow(typeof(EntityOffsets), measured.Components),
                Follow(typeof(EntityDetails), measured.LookUp),
                Follow(typeof(ComponentLookUpStruct), measured.NameMap));
            var search = new EntityLayoutSearch(memory);
            if (entities.Any(entity => search.ReadsPath(entity, now)))
            {
                return false;
            }

            var outcomes = JudgeEntityLayout(memory, entities);
            foreach (var outcome in outcomes.Where(outcome => outcome.Health != StructHealth.Intact))
            {
                Console.WriteLine($"[OffsetHealer] {outcome.Name}: {outcome.Health} from +0x{outcome.From:X} to +0x{outcome.To:X}");
            }

            return outcomes.Any(outcome => outcome.Health == StructHealth.Moved);
        }

        /// <summary>
        ///     Judges the UI element base fields on <paramref name="root"/> (<see cref="UiElementLayoutSearch"/>) and reads every
        ///     element from there. Empty when it is no UI root (no self pointer, no element of the base resolution's size).
        /// </summary>
        /// <summary>
        ///     Reads pointer fields of a struct from where a search of the plugin found them, each (measured, found); what
        ///     follows each pointer stays where it reads now.
        /// </summary>
        public static void PlacePointers(Type structType, params (int Measured, int Found)[] fields)
        {
            var closing = fields.Select(field => field.Measured + IntPtr.Size).Where(after => fields.All(field => field.Measured != after))
                .Select(after => (after, Follow(structType, after)));
            Place(structType, fields.Concat(closing).ToArray());
        }

        /// <summary>Where the UI element base fields are on <paramref name="root"/>, searched nearest <paramref name="near"/>; moves nothing.</summary>
        public static UiElementFields FindUiElementBase(IntPtr root, UiElementFields near) =>
            new UiElementLayoutSearch(new GameMemory(Core.Process.Handle)).Find(root.ToInt64(), near);

        public static IReadOnlyList<StructOutcome> JudgeUiElementBase(IntPtr root)
        {
            var measured = UiElementFields.Measured;
            var found = FindUiElementBase(root, measured);
            if (found.Self < 0 || found.Size < 0)
            {
                return Array.Empty<StructOutcome>();
            }

            Place(
                typeof(UiElementBaseOffset),
                (measured.Self, found.Self),
                (measured.Children, found.Children),
                (measured.Parent, found.Parent),
                (measured.Position, found.Position),
                (measured.Flags, found.Flags),
                (measured.Size, found.Size));
            return new[]
            {
                Outcome(ElementTreePart, (measured.Self, found.Self), (measured.Children, found.Children), (measured.Parent, found.Parent)),
                Outcome(ElementRectsPart, (measured.Position, found.Position), (measured.Size, found.Size)),
                Outcome(ElementFlagsPart, (measured.Flags, found.Flags)),
            };
        }

        /// <summary>Reads the game state object's stack and state list from where <paramref name="found"/> says; their outcomes.</summary>
        internal static IReadOnlyList<StructOutcome> PlaceGameStates(GameStateFields found)
        {
            var measured = GameStateFields.Measured;
            Place(typeof(GameStateOffset), (measured.Stack, found.Stack), (measured.States, found.States));
            return new[] { Outcome(StateStackPart, (measured.Stack, found.Stack)), Outcome(StateListPart, (measured.States, found.States)) };
        }

        /// <summary>A part's outcome over its fields, each (measured, found); the first that moved says where.</summary>
        internal static StructOutcome Outcome(string name, params (int Measured, int Found)[] fields)
        {
            if (fields.Any(field => field.Found < 0))
            {
                return new StructOutcome(name, StructHealth.Lost, fields[0].Measured, 0, "nothing nearby holds what it must");
            }

            var moved = fields.FirstOrDefault(field => field.Found != field.Measured);
            return moved == default ? new StructOutcome(name, StructHealth.Intact, fields[0].Measured, fields[0].Measured, string.Empty)
                : new StructOutcome(name, StructHealth.Moved, moved.Measured, moved.Found, string.Empty);
        }

        /// <summary>
        ///     Reads each field (measured, found) of a struct from where it was found, one not found from where it reads now;
        ///     true when that changed anything. What memory shows replaces what the code had placed.
        /// </summary>
        private static bool Place(Type structType, params (int Measured, int Found)[] fields)
        {
            var segments = fields.Select(field => new RemapSegment(field.Measured, (field.Found < 0 ? Follow(structType, field.Measured) : field.Found) - field.Measured)).ToArray();
            lock (Healed)
            {
                if (FromCode.TryGetValue(structType, out var fromCode))
                {
                    fromCode.ExceptWith(segments.Select(segment => segment.From));
                }
            }

            if (segments.All(segment => Follow(structType, segment.From) == segment.From + segment.Shift))
            {
                return false;
            }

            Remap(structType, Merged(structType, segments));
            return true;
        }

        /// <summary>
        ///     Puts a struct's fields where the game's code reads them (<see cref="CodeFieldSearch"/>). A segment a live
        ///     judgment placed stays: what memory shows beats what the code suggests.
        /// </summary>
        /// <param name="structName">The <c>GameOffsets</c> struct.</param>
        /// <param name="fields">Each field, where the signatures were made for it, and where the code reads it now.</param>
        /// <returns>
        ///     Each field's outcome; null, moving nothing, when a field is unknown or not where its signatures were made:
        ///     a wrong line in the catalog must not move a right field.
        /// </returns>
        public static IReadOnlyList<StructOutcome>? PlaceFromCode(string structName, IReadOnlyList<(string Field, int Expected, int Found)> fields)
        {
            if (typeof(EntityOffsets).Assembly.GetTypes().FirstOrDefault(type => type.Name == structName) is not { } structType)
            {
                return null;
            }

            var outcomes = new List<StructOutcome>(fields.Count);
            var placed = new List<RemapSegment>(fields.Count);
            foreach (var (field, expected, found) in fields)
            {
                if (OffsetOfPath(structType, field) is not { } measured || measured != expected)
                {
                    return null;
                }

                placed.Add(new RemapSegment(measured, found - measured));
                outcomes.Add(new StructOutcome(field, found == measured ? StructHealth.Intact : StructHealth.Moved, measured, found, string.Empty));
            }

            lock (Healed)
            {
                var kept = Healed.TryGetValue(structType, out var segments) ? segments : Array.Empty<RemapSegment>();
                var fromCode = FromCode.TryGetValue(structType, out var known) ? known : new HashSet<int>();
                var live = kept.Where(segment => !fromCode.Contains(segment.From)).ToList();
                var wanted = placed.Where(segment => live.All(other => other.From != segment.From)).ToList();
                if (wanted.Any(segment => segment.Shift != 0) || fromCode.Count > 0)
                {
                    FromCode[structType] = wanted.Select(segment => segment.From).ToHashSet();
                    Remap(structType, StructRemapper.Segments(live.Concat(wanted).OrderBy(segment => segment.From)));
                }
            }

            return outcomes;
        }

        /// <summary>A field's place in a struct, through the nested structs of a dotted path; null when there is no such field.</summary>
        private static int? OffsetOfPath(Type structType, string path)
        {
            var offset = 0;
            foreach (var name in path.Split('.'))
            {
                if (structType.GetField(name) is not { } field)
                {
                    return null;
                }

                offset += Marshal.OffsetOf(structType, name).ToInt32();
                structType = field.FieldType;
            }

            return offset;
        }

        /// <summary>Sets where a struct's fields moved.</summary>
        internal static void Remap(Type structType, RemapSegment[] segments)
        {
            lock (Healed)
            {
                Healed[structType] = segments;
            }

            typeof(StructRemap<>).MakeGenericType(structType)
                .GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { segments });

            // ItemStruct is the head of EntityOffsets, read alone by the item and path readers: it moves with it.
            if (structType == typeof(EntityOffsets))
            {
                Remap(typeof(ItemStruct), segments.Where(segment => segment.From < Marshal.SizeOf<ItemStruct>()).ToArray());
            }
        }

        private static int OffsetOf<T>(string field) => Marshal.OffsetOf<T>(field).ToInt32();

        private static byte[] Block(IntPtr address, int size) => Core.Process.Handle.ReadMemoryArray<byte>(address, size);

        /// <summary>Bytes of the game, for a search the plugin runs.</summary>
        public static byte[] ReadBlock(IntPtr address, int size) => Block(address, size);

        /// <summary>The outcome of a field found at <paramref name="found"/>, moving what follows it in the struct.</summary>
        private static StructOutcome Placed<T>(string name, int measured, int found)
        {
            if (found < 0)
            {
                return new StructOutcome(name, StructHealth.Lost, measured, 0, "nothing nearby holds what it must");
            }

            if (found == measured)
            {
                Unmove(typeof(T), measured);
                return new StructOutcome(name, StructHealth.Intact, measured, found, string.Empty);
            }

            Remap(typeof(T), Merged(typeof(T), new RemapSegment(measured, found - measured)));
            return new StructOutcome(name, StructHealth.Moved, measured, found, string.Empty);
        }

        /// <summary>A field found where it was measured reads from there, whatever a repair of another build left behind.</summary>
        private static void Unmove(Type structType, int measured)
        {
            if (Follow(structType, measured) != measured)
            {
                Remap(structType, Merged(structType, new RemapSegment(measured, 0)));
            }
        }

        /// <summary>The struct's segments with <paramref name="moved"/> in them: a struct can have fields moved apart.</summary>
        private static RemapSegment[] Merged(Type structType, params RemapSegment[] moved)
        {
            lock (Healed)
            {
                var kept = Healed.TryGetValue(structType, out var segments) ? segments : Array.Empty<RemapSegment>();
                return StructRemapper.Segments(kept.Where(segment => moved.All(other => other.From != segment.From)).Concat(moved));
            }
        }

        private static StructOutcome NotJudged(string name, string why) => new(name, StructHealth.NotJudged, 0, 0, why);

        /// <summary>A pointer of the game state moved: finds the one <paramref name="fits"/> accepts and reads that one alone from there.</summary>
        private static bool LocatePointer(IntPtr gameState, string field, Func<long, bool> fits)
        {
            var measured = OffsetOf<InGameStateOffset>(field);
            var block = Block(gameState, GameStateBytes);
            var found = StructShapes.Nearest(measured, sizeof(long), block.Length - sizeof(long), at => fits(BitConverter.ToInt64(block, at)));
            if (found < 0)
            {
                return false;
            }

            if (found != measured)
            {
                lock (Healed)
                {
                    var segments = Healed.TryGetValue(typeof(InGameStateOffset), out var known) ? known.ToList() : new List<RemapSegment>();
                    segments.RemoveAll(segment => segment.From == measured || segment.From == measured + sizeof(long));
                    segments.Add(new RemapSegment(measured, found - measured));
                    segments.Add(new RemapSegment(measured + sizeof(long), 0));
                    Remap(typeof(InGameStateOffset), segments.OrderBy(segment => segment.From).ToArray());
                }
            }

            return true;
        }

        private static StructOutcome JudgeCamera(IntPtr world)
        {
            const string name = "camera";
            var measured = OffsetOf<WorldDataOffset>(nameof(WorldDataOffset.CameraStructurePtr)) +
                           OffsetOf<CameraStructure>(nameof(CameraStructure.WorldToScreenMatrix));

            int FindMatrix(IntPtr address)
            {
                var block = Block(address, measured + SearchRadius);
                return block.Length == 0 ? -1 : StructShapes.Nearest(measured, sizeof(int), block.Length - 1, at => StructShapes.IsCameraMatrix(block, at));
            }

            var found = FindMatrix(world);
            if (found < 0 && LocatePointer(Core.States.InGameStateObject.Address, nameof(InGameStateOffset.WorldData), pointer => FindMatrix(new IntPtr(pointer)) >= 0))
            {
                return new StructOutcome(name, StructHealth.Moved, measured, measured, "the world pointer moved");
            }

            return Placed<WorldDataOffset>(name, measured, found);
        }

        private static StructOutcome JudgeLife(Entity player)
        {
            const string name = "life";
            if (!player.TryGetComponent<Life>(out var life))
            {
                return NotJudged(name, "no player");
            }

            string[] fields = { nameof(LifeOffset.Health), nameof(LifeOffset.Mana), nameof(LifeOffset.EnergyShield), nameof(LifeOffset.Ward), nameof(LifeOffset.Divinity) };
            var measured = fields.Select(field => OffsetOf<LifeOffset>(field)).ToArray();
            var block = Block(life.Address, measured[^1] + SearchRadius);
            var (back, total) = (OffsetOf<VitalStruct>(nameof(VitalStruct.PtrToLifeComponent)), OffsetOf<VitalStruct>(nameof(VitalStruct.Total)));
            var currentAfterTotal = OffsetOf<VitalStruct>(nameof(VitalStruct.Current)) - total;
            var vitals = StructShapes.Vitals(block, life.Address.ToInt64(), back, total, total + currentAfterTotal);
            var (foundBack, foundTotal) = (back, total);
            // Enough vitals and none full may be totals read one field late (a current, then whatever follows): full ones decide.
            var readable = vitals.Count >= MinVitals;
            if ((!readable || StructShapes.Full(block, vitals, total, currentAfterTotal) == 0) &&
                StructShapes.VitalLayout(block, life.Address.ToInt64(), back, total, currentAfterTotal, MinVitals, readable) is { } inside)
            {
                // A vital that grew or shrank inside: its back-pointer and its total are found again, each vital's start with them.
                (foundBack, foundTotal) = inside;
                vitals = StructShapes.Vitals(block, life.Address.ToInt64(), foundBack, foundTotal, foundTotal + currentAfterTotal);
            }

            var placed = StructShapes.PlaceVitals(measured, vitals);
            if (placed.Length == 0)
            {
                return new StructOutcome(name, StructHealth.Lost, measured[0], 0, "no vitals found");
            }

            // The living player always has a life total, and its life never exceeds it.
            var (healthTotal, healthCurrent) = (BitConverter.ToInt32(block, placed[0] + foundTotal), BitConverter.ToInt32(block, placed[0] + foundTotal + currentAfterTotal));
            if (healthTotal <= 0 || healthCurrent > healthTotal)
            {
                return new StructOutcome(name, StructHealth.Lost, measured[0], 0, $"life reads {healthCurrent} of {healthTotal}");
            }

            if (placed.SequenceEqual(measured) && (foundBack, foundTotal) == (back, total))
            {
                return new StructOutcome(name, StructHealth.Intact, measured[0], measured[0], string.Empty);
            }

            // Each vital: its start, its back-pointer (the reserves after it follow) and its regeneration, total and current.
            var regeneration = OffsetOf<VitalStruct>(nameof(VitalStruct.Regeneration)) - total;
            Remap(typeof(LifeOffset), StructRemapper.Segments(measured.SelectMany((at, index) => new[]
            {
                new RemapSegment(at, placed[index] - at),
                new RemapSegment(at + back, placed[index] + foundBack - (at + back)),
                new RemapSegment(at + total + regeneration, placed[index] + foundTotal - (at + total)),
            })));
            return (foundBack, foundTotal) == (back, total) ? new StructOutcome(name, StructHealth.Moved, measured[0], placed[0], string.Empty)
                : new StructOutcome(name, StructHealth.Moved, total, foundTotal, "inside each vital");
        }

        private static StructOutcome JudgeBuffs(Entity player, IAreaMemory memory)
        {
            const string name = "buffs";
            if (!player.TryGetComponent<Buffs>(out var buffs))
            {
                return NotJudged(name, "no player");
            }

            var measured = OffsetOf<BuffsOffsets>(nameof(BuffsOffsets.StatusEffectPtr));
            var definition = OffsetOf<StatusEffectStruct>(nameof(StatusEffectStruct.BuffDefinationPtr));
            var block = Block(buffs.Address, measured + SearchRadius);
            var found = StructShapes.Nearest(measured, sizeof(long), block.Length - 1, at => StructShapes.IsBuffList(block, at, memory, definition));

            // With no buff on the player no list reads as one, and that says nothing about the field.
            return found < 0 ? NotJudged(name, "no buff to judge it on") : Placed<BuffsOffsets>(name, measured, found);
        }

        /// <summary>The player's inventories by what they hold (<see cref="InventoryLayoutSearch"/>); each field found is read from there.</summary>
        private static StructOutcome JudgeInventories(AreaInstance area, IAreaMemory memory)
        {
            const string name = "inventories";
            var server = area.ServerDataObject.Address;
            if (server == IntPtr.Zero)
            {
                return NotJudged(name, "no server data");
            }

            var measured = InventoryFields.Measured;
            var found = new InventoryLayoutSearch(memory).Find(server.ToInt64(), measured);
            if (found.Players < 0)
            {
                return new StructOutcome(name, StructHealth.Lost, measured.Players, 0, "no inventory of the player holds an item");
            }

            // Each field, then where the bytes after it read now: a vector is three pointers, the size two ints.
            const int vector = 3 * sizeof(long);
            var moved = Place(typeof(ServerDataOffsets), (measured.Players, found.Players), (measured.Players + vector, Follow(typeof(ServerDataOffsets), measured.Players + vector)));
            moved |= Place(typeof(ServerDataStructure), (measured.Inventories, found.Inventories), (measured.Inventories + vector, Follow(typeof(ServerDataStructure), measured.Inventories + vector)));
            moved |= Place(
                typeof(InventoryStruct),
                (measured.Boxes, found.Boxes),
                (measured.Boxes + sizeof(long), Follow(typeof(InventoryStruct), measured.Boxes + sizeof(long))),
                (measured.Cells, found.Cells),
                (measured.Cells + vector, Follow(typeof(InventoryStruct), measured.Cells + vector)));
            if (moved)
            {
                // The server data reads its inventories once per area: what it read through the old fields is read again.
                area.ServerDataObject.Refresh();
            }

            return Outcome(name, (measured.Players, found.Players), (measured.Inventories, found.Inventories), (measured.Boxes, found.Boxes), (measured.Cells, found.Cells));
        }

        /// <summary>Ground items lead to their item's entity: every one looked at must, through the same field.</summary>
        private static StructOutcome JudgeGroundItems(AreaInstance area, IAreaMemory memory)
        {
            const string name = "ground items";
            var items = area.AwakeEntities.Values.Select(entity => entity.TryGetComponent<WorldItem>(out var item) ? item.Address : IntPtr.Zero)
                .Where(address => address != IntPtr.Zero).Take(EntitiesJudged).ToList();
            if (items.Count == 0)
            {
                return NotJudged(name, "no item on the ground");
            }

            var measured = OffsetOf<WorldItemOffsets>(nameof(WorldItemOffsets.ItemEntityPtr));
            var blocks = items.Select(item => Block(item, measured + SearchRadius)).Where(block => block.Length > 0).ToList();
            var found = blocks.Count == 0 ? -1 : StructShapes.Nearest(measured, sizeof(long), blocks.Min(block => block.Length) - sizeof(long), at => blocks.All(block => StructShapes.IsItemEntity(block, at, memory)));
            return Placed<WorldItemOffsets>(name, measured, found);
        }

        private static StructOutcome JudgeSkills(Entity player, IAreaMemory memory)
        {
            const string name = "skills";
            if (!player.TryGetComponent<Actor>(out var actor))
            {
                return NotJudged(name, "no player");
            }

            var measured = OffsetOf<ActorOffset>(nameof(ActorOffset.ActiveSkillsPtr));
            var block = Block(actor.Address, measured + SearchRadius);
            return Placed<ActorOffset>(name, measured, StructShapes.Nearest(measured, sizeof(long), block.Length - 1, at => StructShapes.IsSkillList(block, at, memory)));
        }

        /// <summary>
        ///     Whether friend and foe still read apart, judged on the player and the monsters around; null with too few of them, or
        ///     while nothing can be said. Only a player who is no friend moves the field: a crowd of minions reads friend as well, and a
        ///     bool that happens to read 1 on the player is not taken for the field until monsters read foe in it.
        /// </summary>
        public static StructOutcome? JudgeReaction(Entity player, IReadOnlyList<IntPtr> monsters)
        {
            if (!player.TryGetComponent<Positioned>(out var own) || monsters.Count < MinMonstersForReaction)
            {
                return null;
            }

            var measured = OffsetOf<PositionedOffsets>(nameof(PositionedOffsets.Reaction));
            var size = measured + SearchRadius;
            var block = Block(own.Address, size);
            if (block.Length < size)
            {
                return null;
            }

            var others = monsters.Select(monster => Block(monster, size)).ToList();
            if (StructShapes.IsReaction(block, others, measured))
            {
                return Placed<PositionedOffsets>(ReactionPart, measured, measured);
            }

            return StructShapes.IsFriendly(block[measured]) ? null
                : Placed<PositionedOffsets>(ReactionPart, measured, StructShapes.FindReaction(block, others, measured, ReactionRadius));
        }

        /// <summary>
        ///     One round of watching the player and <paramref name="enemies"/> act; null until they told where the animation is. Its
        ///     own judgment, apart from the sweep: only actors in motion say it, and upstream's history shows it moving without the
        ///     skills. The player helps find the pair; only enemies count as proof that there is none.
        /// </summary>
        public static StructOutcome? JudgeAnimation(AnimationFieldSearch search, IntPtr player, IEnumerable<IntPtr> enemies)
        {
            var measured = OffsetOf<ActorOffset>(nameof(ActorOffset.AnimationId));
            if (player != IntPtr.Zero)
            {
                search.Add(player.ToInt64(), Block(player, measured + SearchRadius), false);
            }

            foreach (var enemy in enemies)
            {
                search.Add(enemy.ToInt64(), Block(enemy, measured + SearchRadius));
            }

            var found = search.Find(measured);
            if (found < 0)
            {
                return !search.Enough ? null
                    : search.Behaves(measured) ? new StructOutcome(AnimationPart, StructHealth.Intact, measured, measured, "no copy, but it behaves")
                    : new StructOutcome(AnimationPart, StructHealth.Lost, measured, 0, "no two fields agree on what the actors do");
            }

            if (found == measured)
            {
                Unmove(typeof(ActorOffset), measured);
                return new StructOutcome(AnimationPart, StructHealth.Intact, measured, found, string.Empty);
            }

            if (!search.Proven(found))
            {
                return null;
            }

            // Only this field moves: the skills after it stay where they were judged.
            var next = OffsetOf<ActorOffset>(nameof(ActorOffset.ActiveSkillsPtr));
            Remap(typeof(ActorOffset), Merged(typeof(ActorOffset), new RemapSegment(measured, found - measured), new RemapSegment(next, Follow(typeof(ActorOffset), next) - next)));
            return new StructOutcome(AnimationPart, StructHealth.Moved, measured, found, string.Empty);
        }

        private static StructOutcome JudgeStats(Entity player, IAreaMemory memory)
        {
            const string name = "stats";
            if (!player.TryGetComponent<Stats>(out var stats))
            {
                return NotJudged(name, "no player");
            }

            var measured = OffsetOf<StatsOffsets>(nameof(StatsOffsets.StatsChangedByItemsPtr));
            var list = OffsetOf<StatsStructInternal>(nameof(StatsStructInternal.Stats));
            var block = Block(stats.Address, measured + SearchRadius);
            return Placed<StatsOffsets>(name, measured, StructShapes.Nearest(measured, sizeof(long), block.Length - 1, at => StructShapes.IsStatsPointer(block, at, memory, list)));
        }

        private static StructOutcome JudgePosition(AreaInstance area)
        {
            if (!area.Player.TryGetComponent<Render>(out var render))
            {
                return NotJudged(PositionsPart, "no player");
            }

            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var (mapWidth, mapHeight) = (area.TerrainMetadata.TotalTiles.X * WorldPerTile, area.TerrainMetadata.TotalTiles.Y * WorldPerTile);
            var window = Core.Process.WindowArea;
            var centre = new Vector2(window.Width / 2f, window.Height / 2f);
            var measured = OffsetOf<RenderOffsets>(nameof(RenderOffsets.CurrentWorldPosition));
            var block = Block(render.Address, measured + SearchRadius);

            bool Fits(int at)
            {
                if (!StructShapes.IsWorldPosition(block, at, mapWidth, mapHeight))
                {
                    return false;
                }

                var position = MemoryMarshal.Read<StdTuple3D<float>>(block.AsSpan(at));
                var offScreen = Vector2.Abs(world.WorldToScreen(position) - centre);
                return offScreen.X < centre.X * CentreShare && offScreen.Y < centre.Y * CentreShare;
            }

            var found = StructShapes.Nearest(measured, sizeof(int), block.Length - 1, Fits);
            if (found < 0 && StructShapes.IsWorldPosition(block, measured, mapWidth, mapHeight))
            {
                // Nothing near the screen's middle, yet the field still holds a point of the map: it is the camera that is not on the player.
                return NotJudged(PositionsPart, "the camera is not on the player");
            }

            return Placed<RenderOffsets>(PositionsPart, measured, found);
        }

        private static StructOutcome Verified(ProbeResult probe, Type type)
        {
            switch (probe.Verdict)
            {
                case OffsetHelperEngine.ProbeVerdict.Intact:
                    return new StructOutcome(probe.Name, StructHealth.Intact, 0, 0, probe.Detail);
                case OffsetHelperEngine.ProbeVerdict.Degraded:
                    var moves = probe.Recoveries.Where(recovery => !recovery.IsAmbiguous).ToList();
                    if (moves.Count == 0)
                    {
                        return new StructOutcome(probe.Name, StructHealth.Lost, 0, 0, probe.Detail);
                    }

                    Remap(type, Merged(type, moves.Select(move => new RemapSegment(move.ConfiguredOffset, move.Shift)).ToArray()));
                    var first = moves.OrderBy(move => move.ConfiguredOffset).First();
                    return new StructOutcome(probe.Name, StructHealth.Moved, first.ConfiguredOffset, first.CandidateOffset, probe.Detail);
                default:
                    return new StructOutcome(probe.Name, StructHealth.NotJudged, 0, 0, probe.Detail);
            }
        }
    }
}
