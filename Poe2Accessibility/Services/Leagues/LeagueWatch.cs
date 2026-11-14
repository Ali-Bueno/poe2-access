namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        /// <summary>While a charge is being placed: what it would blow follows the stick.</summary>
        private const int PlacingPollMs = 150;

        /// <summary>No fog stat logged yet in the area: the first read is logged, unread or not.</summary>
        private const int FogNotLogged = int.MinValue;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly UiElementReader elements;
        private readonly LeagueNews news = new();
        private readonly PlacementNews placement = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly HashSet<string> loggedMonsters = new(StringComparer.Ordinal);
        private readonly List<(uint Id, ExpeditionFind Find)> covered = new();
        private readonly List<uint> charges = new();
        private long lastPoll;
        private bool placing;
        private int? fogLogged = FogNotLogged;

        public LeagueWatch(IAccessibilityOutputService output, PluginLog log, UiElementReader elements)
        {
            this.output = output;
            this.log = log;
            this.elements = elements;
        }

        /// <summary>A new area: nothing is known of its objects.</summary>
        public void Reset()
        {
            this.news.Reset();
            this.placement.Reset();
            (this.placing, this.fogLogged) = (false, FogNotLogged);
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
            var (indicator, blocked) = (false, false);
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

                if (LeaguePieces.RuleOf(path) is not { } rule)
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
                this.LogOnce(path, states, icon);
                if (rule.Piece == LeaguePiece.PlacementIndicator)
                {
                    indicator = true;
                    blocked |= ValueOf(states, LeaguePieces.BlockedState) > 0;
                }
                else if (rule.Piece == LeaguePiece.ExpeditionExplosive && ValueOf(states, LeaguePieces.ActivatedState) == 0)
                {
                    this.charges.Add(entity.Id);
                }
                else if (ValueOf(states, LeaguePieces.PlacingRangeState) > 0 && LeaguePieces.FindOf(rule.Piece, icon) is { } find)
                {
                    this.covered.Add((entity.Id, find));
                }

                foreach (var line in this.news.Heard(entity.Id, rule.Piece, states, icon))
                {
                    this.Say(line, entity, states);
                }
            }

            this.Placement(indicator, blocked, now);
            this.Player();
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
                this.output.Say(SpeechKind.Quest, Announcements.ExplosivesPlaced(count));
            }

            this.placing = placingNow;
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

            if (this.news.Fog(inFog) is { } fog)
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
                this.output.Say(SpeechKind.EnemyNames, Announcements.Possessed(this.NameOf(monster)));
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
            this.output.Say(line == LeagueLine.RogueExileFight ? SpeechKind.EnemyNames : SpeechKind.Quest, text);
        }

        /// <summary>The name the game shows over the object, in the client's language; empty without one.</summary>
        private string NameOf(Entity entity) =>
            entity.TryGetComponent<Render>(out var render)
                ? this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty
                : string.Empty;

        /// <summary>Each followed object's states (and icon) as they first read, and as they change: how the rules are confirmed from a session's log.</summary>
        private void LogOnce(string path, List<(string Name, long Value)> states, string icon)
        {
            var line = $"{FileOf(path)}: {string.Join(", ", states.Select(state => $"{state.Name}={state.Value}"))}{(icon.Length > 0 ? $"; icon {icon}" : string.Empty)}";
            if (this.logged.Add(line))
            {
                this.log.Info($"League piece seen: {line}");
            }
        }

        private static string FileOf(string path)
        {
            var mark = path.IndexOf('@');
            return mark < 0 ? path : path[..mark];
        }
    }
}
