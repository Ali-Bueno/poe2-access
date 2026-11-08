namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;
    using GameOffsets.Natives;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;
    using GameReader.Utils;

    /// <summary>One monster's attack as memory shows it this frame; a part not (yet) trusted is null and the old rules decide.</summary>
    /// <param name="Clock">The playing clip's position and speed.</param>
    /// <param name="Events">The playing clip's named events (empty when not read).</param>
    /// <param name="Clip">The clip's name ("attack_01").</param>
    /// <param name="AnimatedObject">The animated object's path ("Metadata/Monsters/…/X.ao").</param>
    /// <param name="Aim">The current action's target; null when idle or not trusted.</param>
    /// <param name="ActionType">The current action's type id (<see cref="AttackClock.ActionId"/>), 0 when idle.</param>
    /// <param name="SkillId">The acting skill's GrantedEffects id, when its keys check out.</param>
    public sealed record AttackSnapshot(AnimationReading? Clock, IReadOnlyList<ClipEvent> Events, string? Clip, string? AnimatedObject, ActionAim? Aim, ushort ActionType, string? SkillId);

    /// <summary>
    ///     Reads a monster's current action and its animated object's AnimationController (docs/research/dodge-timing.md,
    ///     findings 1-3) through the GameOffsets structs, every pointer checked, each part judged live by its own rule
    ///     (<see cref="AttackFieldCheck"/>): a part judged lost is not read again until the area changes.
    /// </summary>
    public sealed class AttackReader
    {
        private const string ControllerComponent = "AnimationController";
        private const string AnimatedObjectSuffix = ".ao";

        /// <summary>An .amd clip record's size (the clip table's stride, exe 0x1D44560 in 26476662).</summary>
        private const int ClipRecordSize = 0x138;

        /// <summary>One clip's event list in the animation data: a StdVector of event pointers.</summary>
        private static readonly int ClipEventsSize = Marshal.SizeOf<StdVector>();

        private const int MaxComponents = 64;
        private const int MaxEventSlots = 64;
        private const int MaxChain = 64;
        private const int MaxNameBytes = 64;
        private const int MaxIdChars = 128;

        /// <summary>An action's target place within this of its monster (world units, about two screens) is a place, not garbage.</summary>
        private const float MaxPlaceWorld = 3000f;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly AttackData data;
        private readonly PluginLog log;
        private readonly Dictionary<IntPtr, (string? Path, IntPtr Controller)> animatedObjects = new();
        private readonly Dictionary<(IntPtr Data, int Clip), (string? Name, IReadOnlyList<ClipEvent> Events)> clips = new();
        private readonly Dictionary<uint, (AnimationReading Reading, long At)> lastClock = new();

        public AttackReader(HostMemoryReader memory, UiElementReader elements, AttackData data, PluginLog log)
        {
            (this.memory, this.elements, this.data, this.log) = (memory, elements, data, log);
        }

        /// <summary>Animated +0x350 leads to an object of an .ao with an AnimationController.</summary>
        public AttackFieldCheck AnimatedObjects { get; } = new();

        /// <summary>The controller's position advances at its speed.</summary>
        public AttackFieldCheck Clock { get; } = new();

        /// <summary>Each event's id is the hash of its name.</summary>
        public AttackFieldCheck Events { get; } = new();

        /// <summary>The action's type is one the files list.</summary>
        public AttackFieldCheck Actions { get; } = new();

        /// <summary>The action's target entity is an awake entity at that address.</summary>
        public AttackFieldCheck Targets { get; } = new();

        /// <summary>The action's target place lies near its monster.</summary>
        public AttackFieldCheck Places { get; } = new();

        /// <summary>A new area: objects and clips met before are gone.</summary>
        /// <summary>Whether a live check still waits for its verdict: until then every attack is read, so the checks fill fast.</summary>
        public bool Judging =>
            this.AnimatedObjects.Trust == FieldTrust.Unjudged || this.Clock.Trust == FieldTrust.Unjudged || this.Events.Trust == FieldTrust.Unjudged ||
            this.Actions.Trust == FieldTrust.Unjudged || this.Targets.Trust == FieldTrust.Unjudged || this.Places.Trust == FieldTrust.Unjudged;

        public void Reset()
        {
            this.animatedObjects.Clear();
            this.clips.Clear();
            this.lastClock.Clear();
        }

        /// <param name="id">The monster's entity id.</param>
        /// <param name="actor">Its Actor component.</param>
        /// <param name="animated">Its Animated component, zero when it has none.</param>
        /// <param name="place">Its place, world units.</param>
        /// <param name="worldPerGrid">The area's world units per grid cell.</param>
        /// <param name="entityAt">The awake entity with an id, by address, to check a target.</param>
        public AttackSnapshot Read(uint id, IntPtr actor, IntPtr animated, Vector2 place, float worldPerGrid, Func<uint, IntPtr?> entityAt, long now)
        {
            var (aim, type, skill) = this.Action(actor, place, worldPerGrid, entityAt);
            AnimationReading? clock = null;
            IReadOnlyList<ClipEvent> events = Array.Empty<ClipEvent>();
            string? clip = null, path = null;
            if (this.AnimatedObjects.Trust != FieldTrust.Lost && this.Controller(animated) is { } ao)
            {
                path = ao.Path;
                if (this.Clock.Trust != FieldTrust.Lost && this.Reading(ao.Controller) is var (reading, dataPtr))
                {
                    if (this.lastClock.TryGetValue(id, out var last))
                    {
                        this.Clock.Observe(AttackFieldCheck.ClockAdvances(last.Reading, reading, now - last.At));
                    }

                    this.lastClock[id] = (reading, now);
                    clock = this.Clock.Trust == FieldTrust.Intact ? reading : null;
                    if (dataPtr != IntPtr.Zero && reading.Clip >= 0 && this.Events.Trust != FieldTrust.Lost)
                    {
                        (clip, var read) = this.ClipOf(dataPtr, reading.Clip);
                        events = this.Events.Trust == FieldTrust.Intact ? read : Array.Empty<ClipEvent>();
                    }
                }
            }

            return new AttackSnapshot(clock, events, clip, this.AnimatedObjects.Trust == FieldTrust.Intact ? path : null, aim, type, skill);
        }

        private (ActionAim? Aim, ushort Type, string? Skill) Action(IntPtr actor, Vector2 place, float worldPerGrid, Func<uint, IntPtr?> entityAt)
        {
            if (this.Actions.Trust == FieldTrust.Lost || !this.memory.TryRead<IntPtr>(actor + HostStructs.Follow<ActorOffset>(Field<ActorOffset>(nameof(ActorOffset.CurrentActionPtr))), out var action) ||
                action == IntPtr.Zero || !this.memory.IsValidAddress(action) || !this.memory.TryRead<ActorActionOffsets>(action, out var read))
            {
                return (null, 0, null);
            }

            if (this.data.ActionTypeIds.Count > 0)
            {
                this.Actions.Observe(AttackFieldCheck.ActionTypeKnown(read.ActionTypeId, this.data.ActionTypeIds));
            }

            uint targetId = 0;
            if (this.Targets.Trust != FieldTrust.Lost && read.TargetEntityPtr != IntPtr.Zero)
            {
                var resolves = this.memory.IsValidAddress(read.TargetEntityPtr) &&
                               this.memory.TryRead<uint>(read.TargetEntityPtr + HostStructs.Follow<EntityOffsets>(Field<EntityOffsets>(nameof(EntityOffsets.Id))), out targetId) &&
                               entityAt(targetId) == read.TargetEntityPtr;
                this.Targets.Observe(resolves);
                targetId = resolves ? targetId : 0;
            }

            Vector2? point = null;
            if (this.Places.Trust != FieldTrust.Lost && (read.TargetGridPosition.X != 0 || read.TargetGridPosition.Y != 0))
            {
                var world = new Vector2(read.TargetGridPosition.X, read.TargetGridPosition.Y) * worldPerGrid;
                var plausible = AttackFieldCheck.TargetPointPlausible(world, place, MaxPlaceWorld);
                this.Places.Observe(plausible);
                point = plausible ? world : null;
            }

            var actionsTrusted = this.data.ActionTypeIds.Count == 0 || this.Actions.Trust == FieldTrust.Intact;
            var aim = actionsTrusted && (this.Targets.Trust == FieldTrust.Intact || this.Places.Trust == FieldTrust.Intact)
                ? new ActionAim(this.Targets.Trust == FieldTrust.Intact ? targetId : 0, this.Places.Trust == FieldTrust.Intact ? point : null)
                : (ActionAim?)null;
            return (aim, read.ActionTypeId, this.SkillId(read.SkillPtr));
        }

        /// <summary>The acting skill's GrantedEffects id, through keys the dat checks name (as the player's skills, <see cref="PlayerSkills"/>).</summary>
        private string? SkillId(IntPtr skill) =>
            skill != IntPtr.Zero && this.memory.IsValidAddress(skill) &&
            DatKey.Row(this.memory, this.elements, skill + GameLayout.SkillSlot.LevelKeyOffset, GameLayout.SkillSlot.LevelTable) is { } level &&
            DatKey.Row(this.memory, this.elements, level + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) is { } effect &&
            this.memory.TryRead<IntPtr>(effect + GameLayout.SkillSlot.EffectIdOffset, out var text) && this.elements.ReadUtf16BufferAt(text, MaxIdChars) is { Length: > 0 } id
                ? id
                : null;

        /// <summary>The animated object's path and its AnimationController, read once per object and judged on every read.</summary>
        private (string? Path, IntPtr Controller)? Controller(IntPtr animated)
        {
            if (animated == IntPtr.Zero ||
                !this.memory.TryRead<IntPtr>(animated + HostStructs.Follow<AnimatedOffsets>(Field<AnimatedOffsets>(nameof(AnimatedOffsets.AnimatedObjectPtr))), out var ao) ||
                ao == IntPtr.Zero || !this.memory.IsValidAddress(ao))
            {
                return null;
            }

            if (!this.animatedObjects.TryGetValue(ao, out var found))
            {
                found = this.Components(ao);
                this.animatedObjects[ao] = found;
            }

            var holds = found.Path?.EndsWith(AnimatedObjectSuffix, StringComparison.OrdinalIgnoreCase) == true && found.Controller != IntPtr.Zero;
            this.AnimatedObjects.Observe(holds);
            return holds ? found : null;
        }

        /// <summary>An entity-like object's path and AnimationController, through the same details and name map as an entity's.</summary>
        private (string? Path, IntPtr Controller) Components(IntPtr entity)
        {
            var itemBase = Field<EntityOffsets>(nameof(EntityOffsets.ItemBase));
            if (!this.memory.TryRead<IntPtr>(entity + HostStructs.Follow<EntityOffsets>(itemBase + Field<ItemStruct>(nameof(ItemStruct.EntityDetailsPtr))), out var details) ||
                !this.memory.TryRead<StdWString>(details + HostStructs.Follow<EntityDetails>(Field<EntityDetails>(nameof(EntityDetails.name))), out var pathText) ||
                !this.memory.TryRead<IntPtr>(details + HostStructs.Follow<EntityDetails>(Field<EntityDetails>(nameof(EntityDetails.ComponentLookUpPtr))), out var lookup) ||
                !this.memory.TryRead<StdBucket>(lookup + HostStructs.Follow<ComponentLookUpStruct>(Field<ComponentLookUpStruct>(nameof(ComponentLookUpStruct.ComponentsNameAndIndex))), out var bucket) ||
                !this.memory.TryRead<StdVector>(entity + HostStructs.Follow<EntityOffsets>(itemBase + Field<ItemStruct>(nameof(ItemStruct.ComponentListPtr))), out var list))
            {
                return (null, IntPtr.Zero);
            }

            var path = this.memory.ReadStdWString(pathText);
            var names = this.memory.ReadStdVector<ComponentNameAndIndexStruct>(bucket.Data, MaxComponents);
            var components = this.memory.ReadStdVector<IntPtr>(list, MaxComponents);
            foreach (var entry in names)
            {
                if (entry.Index >= 0 && entry.Index < components.Length && this.Narrow(entry.NamePtr) == ControllerComponent)
                {
                    return (path, components[entry.Index]);
                }
            }

            return (path, IntPtr.Zero);
        }

        private (AnimationReading Reading, IntPtr Data)? Reading(IntPtr controller)
        {
            // Each field where the code says it is now (semantic rules, through the remap), not only where it was measured.
            IntPtr At(string field) => controller + HostStructs.Follow<AnimationControllerOffsets>(Field<AnimationControllerOffsets>(field));
            float Float(string field) => this.memory.TryRead<float>(At(field), out var value) ? value : float.NaN;
            if (!this.memory.TryRead<int>(At(nameof(AnimationControllerOffsets.CurrentClip)), out var clip) ||
                !this.memory.TryRead<IntPtr>(At(nameof(AnimationControllerOffsets.AnimationDataPtr)), out var data))
            {
                return null;
            }

            var off = this.memory.TryRead<byte>(At(nameof(AnimationControllerOffsets.GlobalSpeedOff)), out var flag) && flag != 0;
            var reading = new AnimationReading(clip, Float(nameof(AnimationControllerOffsets.CurrentPosition)), Float(nameof(AnimationControllerOffsets.CurrentSpeed)),
                Float(nameof(AnimationControllerOffsets.GlobalSpeed)), off, Float(nameof(AnimationControllerOffsets.CurrentStart)), Float(nameof(AnimationControllerOffsets.CurrentEnd)));
            return (reading, data != IntPtr.Zero && this.memory.IsValidAddress(data) ? data : IntPtr.Zero);
        }

        /// <summary>A clip's name and named events, read once per animation data and clip; each event judged as it is read.</summary>
        private (string? Name, IReadOnlyList<ClipEvent> Events) ClipOf(IntPtr animationData, int clip)
        {
            if (this.clips.TryGetValue((animationData, clip), out var known))
            {
                return known;
            }

            string? name = null;
            var events = new List<ClipEvent>();
            if (this.memory.TryRead<AnimationDataOffsets>(animationData, out var data))
            {
                var table = data.ClipTablePtr != IntPtr.Zero ? data.ClipTablePtr
                    : this.memory.TryRead<AmdFileOffsets>(data.AmdFilePtr, out var amd) ? amd.ClipTablePtr : IntPtr.Zero;
                if (table != IntPtr.Zero && this.memory.TryRead<AmdClipTableOffsets>(table, out var clipTable) &&
                    clip < (clipTable.Clips.Last.ToInt64() - clipTable.Clips.First.ToInt64()) / ClipRecordSize &&
                    this.memory.TryRead<StdString>(clipTable.Clips.First + (clip * ClipRecordSize), out var clipName))
                {
                    name = this.Narrow(clipName);
                }

                if (clip < (data.ClipEvents.Last.ToInt64() - data.ClipEvents.First.ToInt64()) / ClipEventsSize &&
                    this.memory.TryRead<StdVector>(data.ClipEvents.First + (clip * ClipEventsSize), out var slots))
                {
                    foreach (var head in this.memory.ReadStdVector<IntPtr>(slots, MaxEventSlots))
                    {
                        this.Chain(head, events);
                    }
                }
            }

            var found = (name, (IReadOnlyList<ClipEvent>)events);
            this.clips[(animationData, clip)] = found;
            return found;
        }

        private void Chain(IntPtr clipEvent, List<ClipEvent> events)
        {
            for (var hop = 0; hop < MaxChain && clipEvent != IntPtr.Zero && this.memory.TryRead<AnimationEventOffsets>(clipEvent, out var read); hop++)
            {
                if (read.Kind == 0)
                {
                    var name = this.Narrow(read.Name);
                    var holds = AttackFieldCheck.EventLayoutHolds(name, read.Id);
                    this.Events.Observe(holds);
                    if (holds && float.IsFinite(read.Time))
                    {
                        events.Add(new ClipEvent(name!, read.Time));
                    }
                }

                clipEvent = read.Next;
            }
        }

        /// <summary>An MSVC std::string: its bytes in place up to 15, else behind its pointer.</summary>
        private string? Narrow(StdString text)
        {
            if (text.Length is <= 0 or > MaxNameBytes || text.Capacity < text.Length)
            {
                return null;
            }

            var raw = new byte[16];
            if (text.Capacity <= 15)
            {
                BitConverter.TryWriteBytes(raw.AsSpan(0, 8), text.Buffer.ToInt64());
                BitConverter.TryWriteBytes(raw.AsSpan(8, 8), text.ReservedBytes.ToInt64());
                return Encoding.ASCII.GetString(raw, 0, text.Length);
            }

            var bytes = this.memory.ReadBytes(text.Buffer, text.Length);
            return bytes.Length == text.Length ? Encoding.ASCII.GetString(bytes) : null;
        }

        /// <summary>A C string (component names).</summary>
        private string? Narrow(IntPtr text)
        {
            var bytes = this.memory.ReadBytes(text, MaxNameBytes);
            var end = Array.IndexOf(bytes, (byte)0);
            return end > 0 ? Encoding.ASCII.GetString(bytes, 0, end) : null;
        }

        private static int Field<T>(string name) => (int)Marshal.OffsetOf<T>(name);
    }
}
