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

        /// <summary>While a fight's clones walk, at each poll of the boss watch (150 ms): seconds decide.</summary>
        private const int ClonePollMs = 100;

        /// <summary>The true clone's place is logged this often (ms) while the player is led to it.</summary>
        private const int CloneLogMs = 500;

        /// <summary>A boss arena around the boss (world units), as <see cref="WeakPointGuide"/> takes it.</summary>
        private const float ArenaWorld = 2000f;

        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly EntityPlaces positions;
        private readonly PluginLog log;
        private readonly BossPlaces places = new();
        private readonly ClonePaths clonePaths = new();
        private readonly SafeSpotGuide safeSpot = new();
        private readonly List<ClonePathMarker> markers = new();
        private readonly RefugeEnd refugeEnd = new();
        private readonly BossHelperWarnings helperWarnings = new();
        private readonly Dictionary<uint, string> names = new();
        private readonly Dictionary<uint, (IReadOnlyList<BossPlaceRule> Rules, string? Helper, (string Stem, ClonePathRole Role)? Marker)> kinds = new();
        private readonly List<BossPlaceSight> sights = new();
        private readonly List<(uint Id, Vector2 At, AttackArea? Area)> helpers = new();
        private (Vector2 At, float Radius)? cloneLead;
        private (uint Id, Vector2 Spot, bool Settled, float? Hole, float Radius)? cloneAim;
        private bool clonesAbout;
        private long lastPoll;
        private long lastCloneLog;

        public BossPlacesWatch(UiElementReader elements, HostMemoryReader memory, EntityPlaces positions, PluginLog log)
        {
            this.elements = elements;
            this.memory = memory;
            this.positions = positions;
            this.log = log;
        }

        /// <summary>A skill's danger area from the game's tables (<see cref="AttackData.Area"/>); null when they draw none.</summary>
        public Func<string, AttackArea?> AreaOf { get; set; } = _ => null;

        /// <summary>Where the heartbeat leads (world) and how wide the place is; null when nowhere.</summary>
        public (Vector2 At, float Radius)? Lead => this.places.LeadKind == BossPlaceKind.Clone ? this.cloneLead : this.places.Lead;

        /// <summary>The kind of place the heartbeat leads to; null when nowhere.</summary>
        public BossPlaceKind? LeadKind => this.places.LeadKind;

        /// <summary>The fight's clones are about: they are followed whatever becomes of the boss meanwhile.</summary>
        public bool ClonesAbout => this.clonesAbout;

        /// <summary>A blow is coming that spares only where the true clone stops.</summary>
        public bool SafeSpotUp => this.cloneAim is { Hole: not null };

        /// <summary>A shelter of the fight is up.</summary>
        public bool RefugeUp { get; private set; }

        /// <summary>The fight's shelter ended at the last poll while the player lives: they can move freely again.</summary>
        public bool RefugeOver { get; private set; }

        /// <summary>The helpers whose danger reached the player at the last poll: where each stands and the area that holds the player, when the files draw one.</summary>
        public IReadOnlyList<(Vector2 At, AttackArea? Area)> HelperNews { get; private set; } = Array.Empty<(Vector2, AttackArea?)>();

        public void Reset()
        {
            this.places.Reset();
            this.clonePaths.Reset();
            this.safeSpot.Reset();
            this.markers.Clear();
            this.cloneLead = null;
            this.cloneAim = null;
            this.clonesAbout = false;
            this.refugeEnd.Reset();
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
            this.RefugeOver = false;
            if (fighting.Count == 0)
            {
                if (this.kinds.Count > 0)
                {
                    this.Reset();
                }

                return Array.Empty<string>();
            }

            if ((catalog.Places == 0 && catalog.Daemons == 0) || now - this.lastPoll < (this.clonesAbout ? ClonePollMs : PollMs) || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<string>();
            }

            this.lastPoll = now;
            this.sights.Clear();
            this.helpers.Clear();
            this.markers.Clear();
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
            this.PollClones(catalog, here, now);
            this.RefugeUp = this.sights.Any(sight => sight is { Kind: BossPlaceKind.Refuge or BossPlaceKind.Activate, Active: true });
            this.RefugeOver = this.refugeEnd.Update(this.RefugeUp || this.SafeSpotUp, now) && Seats.Followed.TryGetComponent<Life>(out var life) && life.IsAlive;
            var (lines, told) = (new List<string>(), new List<string>());
            foreach (var news in this.places.Update(this.sights, here, player.ModelBounds.X, sheltered))
            {
                if (news.Kind == BossPlaceKind.Clone)
                {
                    continue; // said by the safe spot's guide, with where it stops
                }

                this.log.Info($"Boss place: {news.Kind} {news.Name} to {news.To.X:0},{news.To.Y:0}, {Vector2.Distance(news.To, here):0} world units from the player");
                (news.Pressing ? lines : told).Add(
                    Announcements.BossPlace(news.Kind, news.Name, ScreenProjection.Octant(player.WorldPosition, news.To - here), heartbeat && news.Pressing));
            }

            this.Told = told;
            return lines;
        }

        /// <summary>The fight's clones: where each walks, and where the true one stops (<see cref="Follow"/> leads there).</summary>
        private void PollClones(BossMechanicsCatalog catalog, Vector2 here, long now)
        {
            var clones = this.sights.Where(sight => sight.Kind == BossPlaceKind.Clone).ToList();
            this.clonesAbout = clones.Count > 0;
            this.clonePaths.Update(this.markers, clones.Select(sight => (sight.Id, sight.At)).ToList(), now);
            var chosen = clones.Where(sight => sight.Active).Select(sight => (BossPlaceSight?)sight).FirstOrDefault();
            var stopped = chosen is { } seen ? this.clonePaths.StoppedMs(seen.Id, now) : 0;
            if (chosen is not { } clone || stopped >= SafeSpotGuide.StaleMs)
            {
                this.safeSpot.Reset();
                this.cloneAim = null;
                this.cloneLead = null;
                this.sights.RemoveAll(sight => sight.Kind == BossPlaceKind.Clone); // its blow is past: no more a place of the fight
                return;
            }

            var end = this.clonePaths.EndOf(clone.Id);
            var rested = stopped >= ClonePaths.SettledMs && (end == null || this.clonePaths.Walked(clone.Id));
            var spot = rested ? clone.At : end ?? clone.At; // where it really stopped says more than where its way ends
            var hole = catalog.AreaSkills(clone.Monster).Select(this.AreaOf)
                .Where(area => area is { Shape: AttackAreas.Donut, Width: > 0f }).Select(area => (float?)area!.Value.Width).FirstOrDefault();
            this.cloneAim = (clone.Id, spot, rested || end != null, hole, clone.Radius);
            if (now - this.lastCloneLog >= CloneLogMs)
            {
                this.lastCloneLog = now;
                this.log.Info($"Boss clone: #{clone.Id} at {clone.At.X:0},{clone.At.Y:0}, {(end is { } to ? $"its way ends at {to.X:0},{to.Y:0}" : "no way fits")} of {this.clonePaths.Count} ({this.clonePaths.Laid} laid), " +
                              $"stopped {stopped} ms, hole {hole?.ToString("0") ?? "?"}, player {here.X:0},{here.Y:0}, {Vector2.Distance(spot, here):0} from it, inside {this.safeSpot.Inside}");
            }
        }

        /// <summary>Each frame: the player's place against where the true clone stops, and the line that leaves no time to wait; null when none.</summary>
        public string? Follow(bool heartbeat, long now)
        {
            if (this.cloneAim is not { } aim || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var radius = player.ModelBounds.X;
            var news = this.safeSpot.Update(aim.Id, aim.Spot, aim.Settled, here, radius, aim.Hole, offset => ScreenProjection.Octant(player.WorldPosition, offset), now);
            this.cloneLead = aim.Hole is not { } hole ? (aim.Spot, aim.Radius) : this.safeSpot.Inside ? null : (aim.Spot, SafeSpotGuide.LeadRadius(hole, radius));
            if (news is not { } said)
            {
                return null;
            }

            this.log.Info($"Boss clone: {said.Kind}, player {here.X:0},{here.Y:0}, {Vector2.Distance(aim.Spot, here):0} from {aim.Spot.X:0},{aim.Spot.Y:0}, settled {aim.Settled}");
            return said.Kind switch
            {
                SafeSpotNewsKind.First => Announcements.BossClone(aim.Hole != null, said.Octant, said.Steps, heartbeat),
                SafeSpotNewsKind.Inside => Announcements.BossCloneReached,
                SafeSpotNewsKind.Out => Announcements.BossCloneLeft(said.Octant, said.Steps),
                _ => Announcements.BossCloneWay(said.Octant, said.Steps),
            };
        }

        private void Note(BossMechanicsCatalog catalog, Entity entity, IReadOnlyList<(string Monster, Vector2 At)> fighting, Vector2 player, float playerRadius)
        {
            if (!this.kinds.TryGetValue(entity.Id, out var kind))
            {
                if (entity.Path.Length == 0)
                {
                    return; // not read yet: asked again at the next poll, never remembered as nothing
                }

                // Asked once per entity: most of an arena is neither a place nor a helper.
                var path = BossMechanicsCatalog.MonsterId(entity.Path);
                this.kinds[entity.Id] = kind = (catalog.PlaceRules(path), catalog.IsDaemon(path) ? path : null, catalog.ClonePath(path));
            }

            if ((kind.Rules.Count == 0 && kind.Helper == null && kind.Marker == null) || this.positions.Of(entity) is not { } at)
            {
                return;
            }

            if (kind.Marker is { } marker)
            {
                this.markers.Add(new ClonePathMarker(entity.Id, marker.Stem, marker.Role, at));
            }

            if (kind.Helper is { } helper)
            {
                this.NoteHelper(catalog, entity.Id, helper, at, fighting, player, playerRadius);
            }

            foreach (var rule in kind.Rules)
            {
                var owner = fighting.Where(boss => string.Equals(boss.Monster, rule.Boss, StringComparison.OrdinalIgnoreCase) && Vector2.Distance(boss.At, at) <= ArenaWorld)
                    .Select(boss => (Vector2?)boss.At).FirstOrDefault();
                if (owner is not { } bossAt)
                {
                    continue;
                }

                // A cover or a marker the game does not draw has a place and neither a name nor a size.
                var (name, radius) = entity.TryGetComponent<Render>(out var render) ? (this.NameOf(entity.Id, render), render.ModelBounds.X) : (string.Empty, 0f);
                this.sights.Add(new BossPlaceSight(entity.Id, rule.Kind, entity.Path, name, at, radius, this.IsActive(entity, rule), bossAt) { Monster = rule.Boss });
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
            AttackArea? holds = areas.Cast<AttackArea?>().FirstOrDefault(area => AttackAreas.Catches(area!.Value, at, BossHelperWarnings.AimOf(area.Value, at, player), player, playerRadius, AttackAim.MarginWorld));
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
            BossPlaceKind.Activate => entity.TryGetComponent<StateMachine>(out var lit) && BossPlaces.StateIs(lit.States.Select(state => (state.Name, state.Value)), rule.State),
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
                if (name.Length > 0)
                {
                    this.names[id] = name; // an empty read is tried again: a name not there yet must not silence the place
                }
            }

            return name;
        }
    }
}
