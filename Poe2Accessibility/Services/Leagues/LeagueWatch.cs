namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Says what a sighted player sees of the league mechanics (<see cref="LeaguePieces"/>): their objects' progress, a possessed
    ///     monster, a shrine's effect, the Delirium fog, what an Expedition charge being placed would blow (docs/research/league-mechanics.md).
    /// </summary>
    public sealed class LeagueWatch
    {
        private const int PollMs = 500;

        /// <summary>A scanner step (world units), as the scanner counts them.</summary>
        private const float StepWorldUnits = 100f;

        /// <summary>While a charge is being placed: what it would blow follows the stick.</summary>
        private const int PlacingPollMs = 150;

        /// <summary>No fog stat logged yet in the area: the first read is logged, unread or not.</summary>
        private const int FogNotLogged = int.MinValue;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly UiElementReader elements;
        private readonly HostMemoryReader memory;
        private readonly LeagueNews news = new();
        private readonly PlacementNews placement = new();
        private readonly PlacementGuide guide = new();
        private readonly EncounterNews tracker = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly HashSet<string> loggedMonsters = new(StringComparer.Ordinal);
        private readonly List<(uint Id, ExpeditionFind Find)> covered = new();
        private readonly List<uint> charges = new();
        private readonly List<bool> findsCovered = new();
        private readonly List<(uint Id, ExpeditionFind Find, Vector2 Place, bool Reached, bool WouldReach)> finds = new();
        private readonly List<Vector2> chargePlaces = new();
        private readonly Dictionary<uint, LeagueRule?> rules = new();
        private readonly Dictionary<uint, (List<(string Name, long Value)> States, string Icon)> lastSeen = new();
        private long lastPoll;
        private bool placing;
        private bool detonated;
        private int? fogLogged = FogNotLogged;

        public LeagueWatch(IAccessibilityOutputService output, PluginLog log, UiElementReader elements, HostMemoryReader memory)
        {
            this.output = output;
            this.log = log;
            this.elements = elements;
            this.memory = memory;
        }

        /// <summary>Gets or sets the lines of the HUD's league encounter tracker as they read now (<see cref="EncounterReader"/>).</summary>
        public Func<IReadOnlyList<string>> TrackerLines { get; set; } = () => Array.Empty<string>();

        /// <summary>Gets or sets the charges of the Expedition site at hand as the client's ruleset counts them: placed, and how many there are.</summary>
        public Func<(int Placed, int Total)?> Charges { get; set; } = () => null;

        /// <summary>Gets or sets whether a Delirium encounter runs, by its ruleset: false once one that ran here is over, null when unknown.</summary>
        public Func<bool?> FogRunning { get; set; } = () => null;

        /// <summary>Gets or sets whether the fog's own reader is speaking of its edges now: it says more than "out of the fog".</summary>
        public Func<bool> FogSpeaks { get; set; } = () => false;

        /// <summary>
        ///     Gets or sets whether the screen shows the placement mode's own legend ("Colocar explosivo", ClientStrings
        ///     ConsoleActionPlaceExplosive): the charge being placed is client-only and not in the entity list (live 2026-11-26).
        /// </summary>
        public Func<bool> PlacingShown { get; set; } = () => false;

        /// <summary>Gets or sets the id of the area the player is in ("ExpeditionLogBook_Reef"): a logbook's charges reach farther.</summary>
        public Func<string> AreaId { get; set; } = () => string.Empty;

        /// <summary>A new area: nothing is known of its objects.</summary>
        public void Reset()
        {
            ExpeditionReach.Publish(new HashSet<uint>());
            this.news.Reset();
            this.placement.Reset();
            this.guide.Reset();
            this.tracker.Reset();
            this.rules.Clear();
            this.lastSeen.Clear();
            ExpeditionReach.PublishDetonated(false);
            (this.placing, this.fogLogged, this.detonated) = (false, FogNotLogged, false);
        }

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < (this.placing ? PlacingPollMs : PollMs))
            {
                return;
            }

            this.lastPoll = now;
            this.covered.Clear();
            this.charges.Clear();
            this.findsCovered.Clear();
            this.finds.Clear();
            this.chargePlaces.Clear();
            var (indicator, blocked) = (false, false);
            Vector2? cursor = null;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid)
                {
                    continue;
                }

                var path = entity.Path ?? string.Empty;
                if (entity.EntityType == EntityTypes.Monster)
                {
                    this.Monster(entity, path);
                }

                if ((this.RuleOf(entity.Id, path) ?? (EssenceWatch.IsCrystal(entity) ? LeaguePieces.EssenceCrystalRule : null)) is not { } rule)
                {
                    continue;
                }

                var states = TransitionState.With(this.memory, entity);
                var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
                var model = rule.Piece == LeaguePiece.ExpeditionMarker ? EntityCatalog.ModelOf(entity) : null;
                if (model != null)
                {
                    icon = LeaguePieces.MarkerIcon(icon, model);
                }

                // A zone's radius and a sacrifice change without the states: those pieces are always looked at.
                if (this.Changed(entity.Id, states, icon) || rule.Piece is LeaguePiece.BreachInitiator or LeaguePiece.RitualAltar)
                {
                    if (model != null)
                    {
                        this.LogOnce(path, new List<(string Name, long Value)> { ("model " + model, 0) }, icon, null, null);
                    }

                    this.LogOnce(path, states, icon, rule.Piece == LeaguePiece.BreachInitiator ? this.ZoneRadius(entity) : null,
                        rule.Piece == LeaguePiece.RitualAltar ? this.Sacrifice(entity) : null);
                }

                var place = entity.TryGetComponent<Render>(out var render) ? new Vector2(render.GridPosition.X, render.GridPosition.Y) : (Vector2?)null;

                if (rule.Piece == LeaguePiece.PlacementIndicator)
                {
                    indicator = true;
                    blocked |= ValueOf(states, LeaguePieces.BlockedState) > 0;
                    cursor = place;
                }
                else if (rule.Piece == LeaguePiece.ExpeditionExplosive && ValueOf(states, LeaguePieces.ActivatedState) == 0)
                {
                    this.charges.Add(entity.Id);
                    if (place is { } charge)
                    {
                        this.chargePlaces.Add(charge);
                    }
                }
                else if (rule.Piece == LeaguePiece.ExpeditionDetonator && ValueOf(states, LeaguePieces.ActivatedState) > 0)
                {
                    this.detonated = true; // the markers are spent, blown or not: they stay in the ground as they were
                }
                else if (!this.detonated && LeaguePieces.FindOf(rule.Piece, icon) is { } find && LeaguePieces.IsListed(rule, states, icon) && place is { } spot)
                {
                    // The game's own word on the find first; the charges' places are asked after the loop, when all are known.
                    this.finds.Add((entity.Id, find, spot, ValueOf(states, LeaguePieces.CoveredState) > 0, ValueOf(states, LeaguePieces.PlacingRangeState) > 0));
                }

                foreach (var line in this.news.Heard(entity.Id, rule.Piece, states, icon))
                {
                    this.Say(line, entity, states);
                }
            }

            ExpeditionReach.PublishDetonated(this.detonated);
            this.Reach(cursor);

            // The tracker first: whether it counts the charges itself decides what a charge placed says.
            foreach (var line in this.tracker.Heard(this.TrackerLines(), now))
            {
                this.output.Say(SpeechKind.Quest, line);
            }

            var mode = !this.detonated && (indicator || this.PlacingShown());
            if (this.placement.Mode(mode) is { } entered)
            {
                this.output.Speak(entered ? Announcements.PlacementModeOn : Announcements.PlacementModeOff, interrupt: true);
            }

            this.Placement(indicator, blocked, now);
            this.Guide(mode, cursor, now);
            this.Player();
        }

        /// <summary>
        ///     Which finds the charges placed reach and which the one being placed would: by the game's own states, and where they say
        ///     nothing by the places and the least radius of a blast (<see cref="ExpeditionReach"/>).
        /// </summary>
        private void Reach(Vector2? cursor)
        {
            if (this.finds.Count == 0)
            {
                ExpeditionReach.Publish(new HashSet<uint>());
                return;
            }

            var radius = ExpeditionReach.Radius(this.AreaId());
            var reached = new HashSet<uint>();
            foreach (var find in this.finds)
            {
                var covered = find.Reached || ExpeditionReach.AnyReaches(this.chargePlaces, find.Place, radius);
                this.findsCovered.Add(covered);
                if (covered)
                {
                    reached.Add(find.Id);
                }

                if (find.WouldReach || (cursor is { } at && ExpeditionReach.Reaches(at, find.Place, radius)))
                {
                    this.covered.Add((find.Id, find.Find));
                }
            }

            ExpeditionReach.Publish(reached);
        }

        private static long ValueOf(List<(string Name, long Value)> states, string name) =>
            states.FirstOrDefault(state => state.Name == name).Value;

        private static bool HasBuff(Buffs buffs, string id)
        {
            foreach (var key in buffs.StatusEffects.Keys)
            {
                if (key == id || BuffIds.Base(key) == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What the charge at the cursor would blow, a refused place and each charge set; silent when the client states are not read.</summary>
        private void Placement(bool indicator, bool blocked, long now)
        {
            var placingNow = indicator || this.covered.Count > 0;
            if (this.placement.Covers(this.covered, placingNow, now) is { } finds)
            {
                this.output.Speak(Announcements.ExpeditionCovers(finds), interrupt: true);
            }

            if (this.placement.Refused(indicator && blocked))
            {
                this.output.Speak(Announcements.ExplosiveRefused, interrupt: true);
            }

            if (this.placement.Placed(this.charges) is { } count)
            {
                // The game's own "Explosivos colocados: 2/5" is said from the tracker when it shows one; ours then adds only what it lacks.
                var text = this.tracker.ShowsCount
                    ? Announcements.FindsUncovered(this.UncoveredKinds())
                    : this.Charges() is { } site
                        ? Announcements.ExplosivesPlaced(site.Placed, site.Total, this.UncoveredKinds())
                        : Announcements.ExplosivesPlaced(count, null, this.UncoveredKinds());
                if (text.Length > 0)
                {
                    this.output.Say(SpeechKind.Quest, text);
                }

                // The last charge: a remnant none reaches will not run its recipe (live 2026-11-26: the one left out went to 8).
                if (this.Charges() is { } all && all.Placed >= all.Total && this.RemnantUncovered())
                {
                    this.output.Say(SpeechKind.Quest, Announcements.RemnantLeftOut);
                }
            }

            this.placing = placingNow;
        }

        private List<(ExpeditionFind Find, int Count)>? UncoveredKinds() =>
            PlacementNews.UncoveredKinds(this.finds.Zip(this.findsCovered, (find, covered) => (find.Find, covered)).ToList());

        private bool RemnantUncovered()
        {
            for (var i = 0; i < this.finds.Count && i < this.findsCovered.Count; i++)
            {
                if (this.finds[i].Find == ExpeditionFind.Remnant && !this.findsCovered[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     While placing and the charge covers nothing: the nearest find no charge reaches, from the player (the charge itself is not
        ///     readable), the most worth first: a remnant, chests, elites and explosive objects before plain monsters.
        /// </summary>
        private void Guide(bool mode, Vector2? charge, long now)
        {
            if (!mode)
            {
                this.guide.Reset();
                return;
            }

            if (charge == null && this.covered.Count == 0 && Seats.Followed.TryGetComponent<Render>(out var self))
            {
                charge = new Vector2(self.GridPosition.X, self.GridPosition.Y);
            }
            else if (charge == null)
            {
                this.guide.Next(null, Array.Empty<(uint, ExpeditionFind, Vector2)>(), now);
                return;
            }

            var open = new List<(uint Id, ExpeditionFind Find, Vector2 Place)>();
            for (var i = 0; i < this.finds.Count && i < this.findsCovered.Count; i++)
            {
                var find = this.finds[i];
                if (!this.findsCovered[i] && !this.covered.Any(piece => piece.Id == find.Id))
                {
                    open.Add((find.Id, find.Find, find.Place));
                }
            }

            if (open.Any(find => PlacementNews.Worth(find.Find) < PlacementNews.Worth(ExpeditionFind.Monsters)))
            {
                open.RemoveAll(find => PlacementNews.Worth(find.Find) >= PlacementNews.Worth(ExpeditionFind.Monsters));
            }

            if (this.guide.Next(charge, open, now) is not { } next || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var steps = (int)Math.Round(next.Offset.Length() * Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor / StepWorldUnits);
            var (alongX, alongY) = ScreenProjection.Axes(player.WorldPosition);
            var direction = Announcements.StickDirections[StickDirection.Octant(alongX, alongY, next.Offset.X, next.Offset.Y)];
            this.output.Say(SpeechKind.Quest, Announcements.ExplosiveNextFind(Announcements.FindName(next.Find), steps, direction));
        }

        /// <summary>The player's own signals: a shrine's buff as it comes on, the Delirium fog while its encounter runs.</summary>
        private void Player()
        {
            var player = Seats.Followed;
            if (player.TryGetComponent<Buffs>(out var buffs))
            {
                foreach (var shrine in this.news.Shrines(buffs.StatusEffects.Keys))
                {
                    this.output.Say(SpeechKind.StatusBegins, Announcements.Shrine(shrine));
                }

                // A lesser shrine by the game's own name of it (catalog/shrines.json) and what its buff does.
                var catalog = ShrineCatalog.Shared;
                foreach (var buff in this.news.LesserShrines(buffs.StatusEffects.Keys, id => catalog.ByBuff(id) is { Lesser: true }))
                {
                    this.output.Say(SpeechKind.StatusBegins, Announcements.LesserShrine(catalog.NameOf(catalog.ByBuff(buff)!, Localization.Language), buff));
                }
            }

            if (!this.news.DeliriumRunning)
            {
                return;
            }

            var inFog = PlayerStats.Of(player, LeaguePieces.FogStat);
            if (inFog != this.fogLogged)
            {
                this.fogLogged = inFog;
                this.log.Info($"Delirium fog stat: {inFog?.ToString() ?? "unread"}"); // whether the game sets it on the player at all
            }

            // The stat falls as the fog fades at an encounter's end: that is no leaving it. And where the fog's reader tells which
            // way it went, "out of the fog" is its line to say.
            if (this.news.Fog(inFog) is { } fog && this.FogRunning() != false && !(fog == LeagueLine.OutOfFog && this.FogSpeaks()))
            {
                if (fog == LeagueLine.OutOfFog)
                {
                    this.output.Urge(Announcements.League(fog, string.Empty, 0));
                }
                else
                {
                    this.output.Say(SpeechKind.Quest, Announcements.League(fog, string.Empty, 0));
                }
            }
        }

        /// <summary>A monster an Azmeri spirit possessed is said once; a mechanic's monsters have their buffs logged once.</summary>
        private void Monster(Entity monster, string path)
        {
            if (!monster.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            var possessed = HasBuff(buffs, LeaguePieces.PossessedBuff);
            if (possessed && EntityCatalog.IsAliveEnemy(monster) && this.news.Possessed(monster.Id))
            {
                this.output.Say(SpeechKind.EnemyNames, Announcements.Possessed(this.NameOf(monster)), happened: true);
            }

            var file = FileOf(path);
            if ((possessed || LeaguePieces.IsMechanicMonster(file)) && this.loggedMonsters.Add(file))
            {
                this.log.Info($"League monster seen: {file}: buffs {string.Join(", ", buffs.StatusEffects.Keys)}");
            }
        }

        private void Say(LeagueLine line, Entity entity, List<(string Name, long Value)> states)
        {
            var named = line is LeagueLine.StrongboxAmbush or LeagueLine.StrongboxReady or LeagueLine.RogueExileFight;
            var text = Announcements.League(line, named ? this.NameOf(entity) : string.Empty, LeaguePieces.WaveOf(states));
            this.output.Say(line == LeagueLine.RogueExileFight ? SpeechKind.EnemyNames : SpeechKind.Quest, text, happened: line == LeagueLine.RogueExileFight);
        }

        /// <summary>The name the game shows over the object, in the client's language; empty without one.</summary>
        private string NameOf(Entity entity) =>
            entity.TryGetComponent<Render>(out var render)
                ? this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty
                : string.Empty;

        /// <summary>The rule of an entity's path, asked once per entity while its path reads.</summary>
        private LeagueRule? RuleOf(uint id, string path)
        {
            if (this.rules.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var rule = LeaguePieces.RuleOf(path);
            if (path.Length > 0)
            {
                this.rules[id] = rule;
            }

            return rule;
        }

        /// <summary>Whether the entity's states or icon differ from the last poll's; remembers them.</summary>
        private bool Changed(uint id, List<(string Name, long Value)> states, string icon)
        {
            if (this.lastSeen.TryGetValue(id, out var before) && before.Icon == icon && before.States.SequenceEqual(states))
            {
                return false;
            }

            this.lastSeen[id] = (states, icon);
            return true;
        }

        /// <summary>Each followed object's states (and icon) as they first read, and as they change: how the rules are confirmed from a session's log.</summary>
        private void LogOnce(string path, List<(string Name, long Value)> states, string icon, int? zoneRadius, string? sacrifice)
        {
            var line = $"{FileOf(path)}: {string.Join(", ", states.Select(state => $"{state.Name}={state.Value}"))}{(icon.Length > 0 ? $"; icon {icon}" : string.Empty)}" +
                       (zoneRadius is { } radius ? $"; zone radius {radius}" : string.Empty) + (sacrifice is { } gone ? $"; sacrifice {gone}" : string.Empty);
            if (this.logged.Add(line))
            {
                this.log.Info($"League piece seen: {line}");
            }
        }

        /// <summary>
        ///     Research: the radius of the object's control zone as a script last set it (<see cref="GameLayout.ControlZoneComponent"/>),
        ///     logged as it changes to learn whether a Breach's ring is kept there; null without the component.
        /// </summary>
        private int? ZoneRadius(Entity entity) =>
            EntityComponents.AddressOf(entity, GameLayout.ControlZoneComponent.Name) is { } zone &&
            this.memory.TryRead<ushort>(zone + GameLayout.ControlZoneComponent.RadiusOffset, out var radius)
                ? (int?)radius
                : null;

        /// <summary>
        ///     Research: a Ritual altar's sacrifice as the Render component holds it (<see cref="GameLayout.RenderComponent.RitualSacrificeOffset"/>),
        ///     in eleven steps whichever its scale, so the log keeps a line per step and not per frame; null when it does not read as a number.
        /// </summary>
        private string? Sacrifice(Entity altar) =>
            altar.TryGetComponent<Render>(out var render) &&
            this.memory.TryRead<float>(render.Address + GameLayout.RenderComponent.RitualSacrificeOffset, out var percent) && float.IsFinite(percent)
                ? (percent <= 1.5f ? percent : MathF.Round(percent / 10f) * 10f).ToString(percent <= 1.5f ? "F1" : "F0", System.Globalization.CultureInfo.InvariantCulture)
                : null;

        private static string FileOf(string path)
        {
            var mark = path.IndexOf('@');
            return mark < 0 ? path : path[..mark];
        }
    }
}
