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
    ///     The places of a fought boss's arena, from the entities the catalog names for it (<see cref="BossMechanicsCatalog.PlaceRules"/>):
    ///     each is said once with its name and the way to it, and <see cref="Lead"/> is where the heartbeat leads (<see cref="BossPlaces"/>).
    /// </summary>
    public sealed class BossPlacesWatch
    {
        private const int PollMs = 450;

        /// <summary>A boss arena around the boss (world units), as <see cref="WeakPointGuide"/> takes it.</summary>
        private const float ArenaWorld = 2000f;

        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly EntityPlaces positions;
        private readonly PluginLog log;
        private readonly BossPlaces places = new();
        private readonly Dictionary<uint, string> names = new();
        private readonly List<BossPlaceSight> sights = new();
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

        /// <summary>Where the heartbeat leads (world) and how wide the place is; null when nowhere.</summary>
        public (Vector2 At, float Radius)? Lead => this.places.Lead;

        /// <summary>A shelter of the fight is up.</summary>
        public bool RefugeUp { get; private set; }

        public void Reset()
        {
            this.places.Reset();
            this.names.Clear();
            this.sights.Clear();
            this.RefugeUp = false;
        }

        /// <summary>After a poll, with the bosses being fought: the lines of the places that turned up.</summary>
        public IReadOnlyList<string> Poll(BossMechanicsCatalog catalog, IReadOnlyList<(string Monster, Vector2 At)> fighting, bool sheltered, bool heartbeat, long now)
        {
            if (fighting.Count == 0)
            {
                if (this.sights.Count > 0 || this.places.Lead != null)
                {
                    this.Reset();
                }

                return Array.Empty<string>();
            }

            if (catalog.Places == 0 || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<string>();
            }

            this.lastPoll = now;
            this.sights.Clear();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.EntityType != EntityTypes.Player)
                {
                    this.Note(catalog, entity, fighting);
                }
            }

            this.RefugeUp = this.sights.Any(sight => sight is { Kind: BossPlaceKind.Refuge, Active: true });
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var lines = new List<string>();
            foreach (var news in this.places.Update(this.sights, fighting[0].At, here, player.ModelBounds.X, this.ReachWorld, sheltered, now))
            {
                this.log.Info($"Boss place: {news.Kind} {news.Name} to {news.To.X:0},{news.To.Y:0}, {Vector2.Distance(news.To, here):0} world units from the player");
                lines.Add(Announcements.BossPlace(news.Kind, news.Name, ScreenProjection.Octant(player.WorldPosition, news.To - here), heartbeat));
            }

            return lines;
        }

        private void Note(BossMechanicsCatalog catalog, Entity entity, IReadOnlyList<(string Monster, Vector2 At)> fighting)
        {
            var rules = catalog.PlaceRules(BossMechanicsCatalog.MonsterId(entity.Path));
            if (rules.Count == 0 || this.positions.Of(entity) is not { } at)
            {
                return;
            }

            // A cover or a marker the game does not draw has a place and neither a name nor a size.
            var (name, radius) = entity.TryGetComponent<Render>(out var render) ? (this.NameOf(entity.Id, render), render.ModelBounds.X) : (string.Empty, 0f);
            foreach (var rule in rules)
            {
                if (!fighting.Any(boss => string.Equals(boss.Monster, rule.Boss, StringComparison.OrdinalIgnoreCase) && Vector2.Distance(boss.At, at) <= ArenaWorld))
                {
                    continue;
                }

                this.sights.Add(new BossPlaceSight(entity.Id, rule.Kind, entity.Path, name, at, radius, this.IsActive(entity, rule)));
                return; // one place per entity: the catalog's first rule for this boss
            }
        }

        private bool IsActive(Entity entity, BossPlaceRule rule) => rule.Kind switch
        {
            BossPlaceKind.Refuge or BossPlaceKind.Clone =>
                entity.TryGetComponent<StateMachine>(out var machine) && machine.States.Any(state => state.Value != 0 && string.Equals(state.Name, rule.State, StringComparison.OrdinalIgnoreCase)),
            BossPlaceKind.Cover => true,
            BossPlaceKind.Use => entity.TryGetComponent<Targetable>(out _) && EntityCatalog.IsInPlay(this.memory, entity),
            _ => entity.TryGetComponent<Life>(out var life) && life.IsAlive && !EntityCatalog.IsFrozenDead(entity) && EntityCatalog.IsInPlay(this.memory, entity),
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
