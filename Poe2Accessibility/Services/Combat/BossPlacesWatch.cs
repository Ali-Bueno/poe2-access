namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>
    ///     A fought boss's arena, from the entities the catalog names for it: its places (<see cref="BossMechanicsCatalog.PlaceRules"/>),
    ///     each said once with its name and the way to it, with <see cref="Lead"/> where the heartbeat leads (<see cref="BossPlaces"/>);
    ///     and its invisible helpers (<see cref="BossMechanicsCatalog.IsDaemon(string, string)"/>), each warned of as its
    ///     danger area reaches the player (<see cref="BossHelperWarnings"/>).
    /// </summary>
    public sealed class BossPlacesWatch
    {
        /// <summary>A helper walks toward the player: looked for this often (ms), between the boss watch's own polls.</summary>
        private const int PollMs = 300;

        /// <summary>A boss arena around the boss (world units), as <see cref="WeakPointGuide"/> takes it.</summary>
        private const float ArenaWorld = 2000f;

        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly EntityPlaces positions;
        private readonly PluginLog log;
        private readonly BossPlaces places = new();
        private readonly BossHelperWarnings helperWarnings = new();
        private readonly Dictionary<uint, string> names = new();
        private readonly Dictionary<uint, (IReadOnlyList<BossPlaceRule> Rules, string? Helper)> kinds = new();
        private readonly List<BossPlaceSight> sights = new();
        private readonly List<(uint Id, Vector2 At, AttackArea? Area)> helpers = new();
        private long lastPoll;

        public BossPlacesWatch(UiElementReader elements, HostMemoryReader memory, EntityPlaces positions, PluginLog log)
        {
            this.elements = elements;
            this.memory = memory;
            this.positions = positions;
            this.log = log;
        }

        /// <summary>The gap within which the player is at a place (the melee reach setting).</summary>
        public float ReachWorld { get; set; } = 40f;

        /// <summary>A skill's danger area from the game's tables (<see cref="AttackData.Area"/>); null when they draw none.</summary>
        public Func<string, AttackArea?> AreaOf { get; set; } = _ => null;

        /// <summary>Where the heartbeat leads (world) and how wide the place is; null when nowhere.</summary>
        public (Vector2 At, float Radius)? Lead => this.places.Lead;

        /// <summary>A shelter of the fight is up.</summary>
        public bool RefugeUp { get; private set; }

        /// <summary>The helpers whose danger reached the player at the last poll: where each stands and the area that holds the player, when the files draw one.</summary>
        public IReadOnlyList<(Vector2 At, AttackArea? Area)> HelperNews { get; private set; } = Array.Empty<(Vector2, AttackArea?)>();

        public void Reset()
        {
            this.places.Reset();
            this.helperWarnings.Reset();
            this.names.Clear();
            this.kinds.Clear();
            this.sights.Clear();
            this.helpers.Clear();
            this.RefugeUp = false;
            this.HelperNews = Array.Empty<(Vector2, AttackArea?)>();
        }

        /// <summary>The things to use or break that turned up at the last poll, told of without urgency.</summary>
        public IReadOnlyList<string> Told { get; private set; } = Array.Empty<string>();

        /// <summary>After a poll, with the bosses being fought: the lines of the places to go to that turned up.</summary>
        public IReadOnlyList<string> Poll(BossMechanicsCatalog catalog, IReadOnlyList<(string Monster, Vector2 At)> fighting, bool sheltered, bool heartbeat, long now)
        {
            this.HelperNews = Array.Empty<(Vector2, AttackArea?)>();
            this.Told = Array.Empty<string>();
            if (fighting.Count == 0)
            {
                if (this.kinds.Count > 0)
                {
                    this.Reset();
                }

                return Array.Empty<string>();
            }

            if ((catalog.Places == 0 && catalog.Daemons == 0) || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<string>();
            }

            this.lastPoll = now;
            this.sights.Clear();
            this.helpers.Clear();
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.EntityType != EntityTypes.Player)
                {
                    this.Note(catalog, entity, fighting, here, player.ModelBounds.X);
                }
            }

            var reached = this.helperWarnings.Update(this.helpers.Select(helper => (helper.Id, helper.Area != null)).ToList(), now);
            this.HelperNews = this.helpers.Where(helper => reached.Contains(helper.Id)).Select(helper => (helper.At, helper.Area)).ToList();
            this.RefugeUp = this.sights.Any(sight => sight is { Kind: BossPlaceKind.Refuge, Active: true });
            var (lines, told) = (new List<string>(), new List<string>());
            foreach (var news in this.places.Update(this.sights, fighting[0].At, here, player.ModelBounds.X, this.ReachWorld, sheltered))
            {
                this.log.Info($"Boss place: {news.Kind} {news.Name} to {news.To.X:0},{news.To.Y:0}, {Vector2.Distance(news.To, here):0} world units from the player");
                (news.Pressing ? lines : told).Add(
                    Announcements.BossPlace(news.Kind, news.Name, ScreenProjection.Octant(player.WorldPosition, news.To - here), heartbeat && news.Pressing));
            }

            this.Told = told;
            return lines;
        }

        private void Note(BossMechanicsCatalog catalog, Entity entity, IReadOnlyList<(string Monster, Vector2 At)> fighting, Vector2 player, float playerRadius)
        {
            if (!this.kinds.TryGetValue(entity.Id, out var kind))
            {
                // Asked once per entity: most of an arena is neither a place nor a helper.
                var path = BossMechanicsCatalog.MonsterId(entity.Path);
                this.kinds[entity.Id] = kind = (catalog.PlaceRules(path), catalog.IsDaemon(path) ? path : null);
            }

            if ((kind.Rules.Count == 0 && kind.Helper == null) || this.positions.Of(entity) is not { } at)
            {
                return;
            }

            if (kind.Helper is { } helper)
            {
                this.NoteHelper(catalog, entity.Id, helper, at, fighting, player, playerRadius);
            }

            foreach (var rule in kind.Rules)
            {
                if (!fighting.Any(boss => string.Equals(boss.Monster, rule.Boss, StringComparison.OrdinalIgnoreCase) && Vector2.Distance(boss.At, at) <= ArenaWorld))
                {
                    continue;
                }

                // A cover or a marker the game does not draw has a place and neither a name nor a size.
                var (name, radius) = entity.TryGetComponent<Render>(out var render) ? (this.NameOf(entity.Id, render), render.ModelBounds.X) : (string.Empty, 0f);
                this.sights.Add(new BossPlaceSight(entity.Id, rule.Kind, entity.Path, name, at, radius, this.IsActive(entity, rule)));
                return; // one place per entity: the catalog's first rule for this boss
            }
        }

        /// <summary>A helper of a fought boss: the first of its skills' areas, centred on it, that holds the player; without any area in the files, whether it is upon the player.</summary>
        private void NoteHelper(BossMechanicsCatalog catalog, uint id, string helper, Vector2 at, IReadOnlyList<(string Monster, Vector2 At)> fighting, Vector2 player, float playerRadius)
        {
            var skills = fighting.Where(boss => catalog.IsDaemon(boss.Monster, helper)).Select(boss => catalog.DaemonSkills(boss.Monster, helper)).FirstOrDefault();
            if (skills == null)
            {
                return; // another boss's helper
            }

            var areas = skills.Select(this.AreaOf).Where(area => area != null).Select(area => area!.Value).ToList();
            AttackArea? holds = areas.Cast<AttackArea?>().FirstOrDefault(area => AttackAreas.Catches(area!.Value, at, at, player, playerRadius, AttackAim.MarginWorld));
            if (holds == null && areas.Count == 0 && Vector2.Distance(at, player) - playerRadius <= BossHelperWarnings.UponWorld)
            {
                holds = BossHelperWarnings.Unknown;
            }

            this.helpers.Add((id, at, holds));
        }

        private bool IsActive(Entity entity, BossPlaceRule rule) => rule.Kind switch
        {
            BossPlaceKind.Refuge or BossPlaceKind.Clone =>
                entity.TryGetComponent<StateMachine>(out var machine) && machine.States.Any(state => state.Value != 0 && string.Equals(state.Name, rule.State, StringComparison.OrdinalIgnoreCase)),
            BossPlaceKind.Cover => true,
            BossPlaceKind.Use => entity.TryGetComponent<Targetable>(out _) && EntityCatalog.IsInPlay(this.memory, entity),
            // A fissure or a marker has life and cannot be struck: only what can be targeted is a thing to break.
            _ => entity.TryGetComponent<Targetable>(out _) && entity.TryGetComponent<Life>(out var life) && life.IsAlive && !EntityCatalog.IsFrozenDead(entity) &&
                 EntityCatalog.IsInPlay(this.memory, entity),
        };

        private string NameOf(uint id, Render render)
        {
            if (!this.names.TryGetValue(id, out var name))
            {
                name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                this.names[id] = name;
            }

            return name;
        }
    }
}
