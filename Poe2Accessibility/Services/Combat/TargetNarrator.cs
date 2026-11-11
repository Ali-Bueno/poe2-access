namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Names what the right stick aims at (D2R target-sight: the game's own target, its name and life), once per
    ///     new target while the stick is pushed. The target is the game's mouse-over entity (GameHelper
    ///     <c>InGameState.MouseOverEntityAddress</c>, mouse play) or, with a controller, the entity the HUD's target display
    ///     names (HUD > entity_info_container > entity_info_display > name_label, the bar at the top of the screen;
    ///     UI dump 2026-10-23): the nearest awake entity with that name, whose life and rarity are said only when no
    ///     other carries the name (<see cref="TargetMatch"/>). Sweeping across a pack interrupts the previous name; a
    ///     first name waits behind what is being said.
    /// </summary>
    public sealed class TargetNarrator
    {
        private const int PollMs = 100;

        /// <summary>D2R crosshair: a monster beeped for stays quiet for this long of being the target.</summary>
        private const int BeepQuietMs = 3000;

        private readonly AudioCueService audio;
        private readonly float[] beep;
        private readonly System.Collections.Generic.Dictionary<IntPtr, long> beeped = new();
        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly MonsterMods mods;
        private readonly TargetNaming naming = new();
        private long lastPoll;

        /// <summary>The target came from the HUD's name and several entities carry it: which of them is aimed at is not known.</summary>
        private bool nameShared;

        private const string DisplayName = "entity_info_display";
        private const string NameLabelName = "name_label";

        /// <summary>How deep under the HUD the target display is searched (it sits 3 levels down).</summary>
        private const int DisplaySearchDepth = 4;

        private readonly EntityNameMemo entityNames = new();
        private IntPtr display;

        public TargetNarrator(AudioCueService audio, HostMemoryReader memory, UiElementReader elements, IAccessibilityOutputService output, PluginLog log)
        {
            this.audio = audio;
            this.beep = CueSynth.Crosshair(AudioCueService.SampleRate);
            this.memory = memory;
            this.elements = elements;
            this.output = output;
            this.log = log;
            this.mods = new MonsterMods(memory, elements);
        }

        /// <summary>Crosshair beep gain (settings).</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Beep when the game's target lands on a living enemy (D2R crosshair).</summary>
        public bool Beep { get; set; } = true;

        /// <summary>Speak the target's name while aiming.</summary>
        public bool Names { get; set; } = true;

        /// <summary>Read a magic or rare monster's modifiers after its name (settings).</summary>
        public bool ModNames { get; set; } = true;

        /// <summary>The in-game HUD layer (ScreenNarrator.HudLayer), where the target display lives.</summary>
        public Func<IntPtr>? HudLayer { get; set; }

        /// <summary>The target display's name label, so the HUD narration leaves the target's name to this class.</summary>
        public IntPtr NameLabel { get; private set; }

        /// <summary>A new area: entity addresses and the HUD are rebuilt.</summary>
        public void Reset()
        {
            this.entityNames.Clear();
            this.display = IntPtr.Zero;
            this.NameLabel = IntPtr.Zero;
        }

        /// <summary>Per frame; <paramref name="aiming"/> while the right stick is pushed (and L3, the scanner, is not held).</summary>
        public void Tick(bool playing, bool aiming)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var hovered = Core.States.InGameStateObject.MouseOverEntityAddress;
            this.nameShared = false;
            if (hovered == IntPtr.Zero)
            {
                (hovered, var alone) = this.HudTarget();
                this.nameShared = hovered != IntPtr.Zero && !alone;
            }
            if (this.naming.Moved(hovered, now))
            {
                this.log.Debug($"Mouse-over entity 0x{hovered.ToInt64():X} (aiming {aiming})");
                return;
            }

            this.BeepOnEnemy(hovered, now);
            if (!this.naming.MayName(hovered, this.Names, aiming, now))
            {
                return;
            }

            var entity = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values.FirstOrDefault(candidate => candidate.Address == hovered);
            if (entity == null || Seats.IsLocal(entity) || this.Describe(entity, this.nameShared) is not { } spoken ||
                this.naming.Say(hovered, spoken, this.nameShared, now) is not { } cut)
            {
                return;
            }

            this.output.Say(SpeechKind.EnemyNames, spoken, cut); // a sweep cuts the last name
            this.log.Debug($"Target -> {entity.Path} \"{spoken}\"{(this.nameShared ? " (name shared: life and rarity not said)" : string.Empty)}");
        }

        /// <summary>
        ///     The nearest entity the HUD's target display could be naming, and whether it is the only one; zero when the
        ///     display is hidden or unknown.
        /// </summary>
        private (IntPtr Address, bool Alone) HudTarget()
        {
            if (!this.FindDisplay() || !this.elements.TryReadElement(this.display, out var info) || !this.elements.IsVisible(info) ||
                !this.elements.TryReadText(this.NameLabel, out var name) || name.Length == 0)
            {
                return (IntPtr.Zero, false);
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return (IntPtr.Zero, false);
            }

            var here = new System.Numerics.Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var carriers = new System.Collections.Generic.List<(IntPtr, string, float)>();
            var setAside = new System.Collections.Generic.List<(IntPtr, string, float)>();
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || Seats.IsLocal(entity) || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var entityName = this.entityNames.Of(entity.Address, entity.Id,
                    () => this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset));

                // A corpse or a monster set aside is not what the game targets: it neither is the target nor shares its name.
                if (entityName == name && (entity.EntityType != EntityTypes.Monster || (EntityCatalog.IsAliveEnemy(entity) && !EntityCatalog.IsHidden(entity))))
                {
                    // Nor does what cannot be targeted share it with what can (TargetMatch.Pick).
                    var carrier = (entity.Address, entityName, System.Numerics.Vector2.DistanceSquared(new System.Numerics.Vector2(render.WorldPosition.X, render.WorldPosition.Y), here));
                    (EntityCatalog.IsInPlay(this.memory, entity) ? carriers : setAside).Add(carrier);
                }
            }

            return TargetMatch.Pick(name, carriers, setAside);
        }

        /// <summary>Finds (once per HUD) the target display and its name label by their control names.</summary>
        private bool FindDisplay()
        {
            if (this.display != IntPtr.Zero && this.elements.IsElement(this.display))
            {
                return this.NameLabel != IntPtr.Zero;
            }

            this.display = IntPtr.Zero;
            this.NameLabel = IntPtr.Zero;
            var hud = this.HudLayer?.Invoke() ?? IntPtr.Zero;
            var level = new System.Collections.Generic.List<IntPtr> { hud };
            for (var depth = 0; depth < DisplaySearchDepth && hud != IntPtr.Zero && this.display == IntPtr.Zero; depth++)
            {
                var next = new System.Collections.Generic.List<IntPtr>();
                foreach (var parent in level)
                {
                    if (!this.elements.TryReadElement(parent, out var parentInfo))
                    {
                        continue;
                    }

                    foreach (var child in this.elements.ReadChildren(parentInfo))
                    {
                        if (this.elements.TryReadElement(child, out var childInfo) && childInfo.Name == DisplayName)
                        {
                            this.display = child;
                            this.NameLabel = this.elements.ReadChildren(childInfo).FirstOrDefault(label =>
                                this.elements.TryReadElement(label, out var labelInfo) && labelInfo.Name == NameLabelName);
                            break;
                        }

                        next.Add(child);
                    }
                }

                level = next;
            }

            if (this.display != IntPtr.Zero)
            {
                this.log.Debug($"Target display at 0x{this.display.ToInt64():X}, name label 0x{this.NameLabel.ToInt64():X}");
            }

            return this.NameLabel != IntPtr.Zero;
        }

        /// <summary>Two blips the first time a living enemy becomes the target, then quiet for it while it stays one.</summary>
        private void BeepOnEnemy(IntPtr hovered, long now)
        {
            if (!this.Beep || hovered == IntPtr.Zero)
            {
                return;
            }

            if (this.beeped.TryGetValue(hovered, out var at) && now - at < BeepQuietMs)
            {
                this.beeped[hovered] = now; // still the target: stays quiet
                return;
            }

            var entity = Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values.FirstOrDefault(candidate => candidate.Address == hovered);
            if (entity == null || entity.EntityType != EntityTypes.Monster || !EntityCatalog.IsAliveEnemy(entity))
            {
                return;
            }

            this.beeped[hovered] = now;
            if (this.beeped.Count > 64)
            {
                foreach (var stale in this.beeped.Where(pair => now - pair.Value > BeepQuietMs).Select(pair => pair.Key).ToList())
                {
                    this.beeped.Remove(stale);
                }
            }

            var pan = 0f;
            if (entity.TryGetComponent<Render>(out var render) &&
                Seats.Followed.TryGetComponent<Render>(out var player))
            {
                var offset = new System.Numerics.Vector2(render.WorldPosition.X - player.WorldPosition.X, render.WorldPosition.Y - player.WorldPosition.Y);
                pan = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, offset)).Pan;
            }

            this.audio.Play(this.beep, pan, 1f, this.Gain, "crosshair");
        }

        /// <summary>
        ///     Name, rarity of a magic or better monster, life when hurt; null for unnamed things and corpses. The name alone
        ///     when <paramref name="nameOnly"/>: the entity stands for one of several of that name.
        /// </summary>
        private string? Describe(Entity entity, bool nameOnly)
        {
            if (!entity.TryGetComponent<Render>(out var render) ||
                this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is not { Length: > 0 } name)
            {
                return null;
            }

            if (entity.EntityType != EntityTypes.Monster)
            {
                return EntityCatalog.IsInPlay(this.memory, entity) ? name : null;
            }

            if (!EntityCatalog.IsAliveEnemy(entity) || !entity.TryGetComponent<Life>(out var life))
            {
                return null;
            }

            if (nameOnly)
            {
                return Announcements.Target(name, null);
            }

            if (entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity != Rarity.Normal)
            {
                name = Announcements.EnemyWithRarity(name, magic.Rarity);
                if (this.ModNames)
                {
                    name = Announcements.WithMods(name, this.mods.Of(magic));
                }
            }

            return Announcements.Target(name, life.Health.Total > 0 ? life.Health.CurrentInPercent() : null);
        }
    }
}
