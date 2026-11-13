namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Gameplay ticks, warnings and readouts of <see cref="Poe2AccessibilityCore"/>, each service isolated from the others' faults.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>A panel's first texts name it in the log.</summary>
        private const int PanelTextsLogged = 3;

        private readonly WorldGate worldGate = new();
        private IntPtr panelSeen;

        /// <summary>
        ///     Per frame: radar, enemy sounds and what is nearby, only in gameplay. Each service runs on its own (D2R
        ///     crash containment): one that throws is logged (throttled per service) and the others still run.
        /// </summary>
        private void TickSounds()
        {
            bool inWorld, playing;
            try
            {
                inWorld = this.WorldInFront() && Core.Process.Foreground && this.InGame;
                playing = inWorld && IsPlayerAlive();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault("gameplay state", ex);
                return;
            }

            // Each part is a static lambda given the plugin and the frame's facts (Isolated): nothing is allocated to run it.
            this.Isolated("enemy sounds", playing, static (core, playing) =>
            {
                if (core.enemyCues != null)
                {
                    core.enemyCues.Enemies = core.Settings.EnemySounds;
                    core.enemyCues.Friends = core.Settings.NpcSounds;
                    core.enemyCues.Players = core.Settings.PlayerSounds;
                    core.enemyCues.Tick(playing && (core.Settings.EnemySounds || core.Settings.NpcSounds || core.Settings.MeleeReach));
                }
            });
            this.Isolated("radar", playing, static (core, playing) => core.radar?.Tick(playing && core.Settings.Radar));
            this.Isolated("partner radar", inWorld, static (core, inWorld) => core.partnerRadar?.Tick(
                inWorld && core.Settings.Radar && Seats.Partner is { } partner && IsAlive(partner)));
            this.Isolated("wall tones", playing, static (core, playing) => core.wallTones?.Tick(playing && core.Settings.WallTones));
            this.Isolated("fights", playing, static (core, playing) => core.fights?.Tick(playing));
            this.Isolated("nearby", playing, static (core, playing) => core.nearby?.Tick(playing, core.Settings.AnnounceNearby, core.Settings.AnnounceInteractTarget));
            this.Isolated("places", playing, static (core, playing) => core.places?.Tick(playing, core.Settings.AnnouncePlaces));
            this.Isolated("exploration", playing, static (core, playing) =>
            {
                if (playing)
                {
                    core.entities?.Track();
                }
            });
            this.Isolated("beacon", playing, static (core, playing) =>
            {
                if (core.beacon != null)
                {
                    core.beacon.TurnPrompts = core.Settings.RoutePrompts;
                    core.GuideToTimed(playing);
                    core.beacon.Tick(playing);
                }
            });
            this.Isolated("target", playing, static (core, playing) =>
            {
                var aiming = !core.padState.IsDown(GameLayout.PadButtons.LeftStick) && !(core.commands?.InSettingsMenu ?? false) &&
                             MathF.Max(MathF.Abs(core.padState.RightX), MathF.Abs(core.padState.RightY)) >= AimDeflection;
                if (core.targets != null)
                {
                    core.targets.Names = core.Settings.AnnounceTarget;
                    core.targets.ModNames = core.Settings.AnnounceMonsterMods;
                    core.targets.Beep = core.Settings.CrosshairBeep;
                    core.targets.Tick(playing && (core.Settings.AnnounceTarget || core.Settings.CrosshairBeep), aiming);
                }
            });
            if (this.InGame)
            {
                this.Isolated("pick-ups", playing, static (core, playing) => core.pickups?.Tick(playing && core.Settings.AnnouncePickups));
                this.Isolated("hand", static core => core.pickups?.TickHand(core.Settings.AnnounceHand));
                this.Isolated("moves", playing, static (core, playing) => core.pickups?.TickMoves(playing, core.Settings.AnnounceMoves));
                this.Isolated("gold", playing, static (core, playing) => core.gold?.Tick(core.focus?.LayerContainer ?? IntPtr.Zero, playing, core.Settings.AnnouncePickups));
                this.Isolated("flasks", playing, static (core, playing) => core.flasks?.Tick(playing, core.Settings.AnnounceFlasks));
                if (Seats.Partner is { } partner)
                {
                    // The partner's own pick-ups, hand, moves and flasks, named (its menus are read for whoever moves in them).
                    var partnerPlaying = inWorld && IsAlive(partner);
                    this.Isolated("partner pick-ups", partnerPlaying, static (core, playing) => core.partnerPickups?.Tick(playing && core.Settings.AnnouncePickups));
                    this.Isolated("partner hand", static core => core.partnerPickups?.TickHand(core.Settings.AnnounceHand));
                    this.Isolated("partner moves", partnerPlaying, static (core, playing) => core.partnerPickups?.TickMoves(playing, core.Settings.AnnounceMoves));
                    this.Isolated("partner flasks", partnerPlaying, static (core, playing) => core.partnerFlasks?.Tick(playing, core.Settings.AnnounceFlasks));
                }

                this.Isolated("barriers", static core => core.barriers?.Tick(core.Settings.AnnounceDiscoveries));
            }

            this.Isolated("bosses", playing, static (core, playing) => core.TickBosses(playing));
            this.Isolated("weak point", playing, static (core, playing) => core.weakPoint?.Tick(playing && core.Settings.BossWeakPoint));
            this.Isolated("boss arena", playing, static (core, playing) => core.arenaLog?.Tick(playing));
            this.Isolated("hazards", playing, static (core, playing) => core.hazards?.Tick(playing && core.Settings.HazardAlarm));
            this.Isolated("shield", playing, static (core, playing) => core.shield?.Tick(playing, core.Settings.ShieldSounds, core.Settings.AnnounceStunBar));
            this.Isolated("perfect timing", playing, static (core, playing) => core.perfectTiming?.Tick(playing, core.Settings.PerfectTimingCue));
            this.Isolated("cull", playing, static (core, playing) => core.culls?.Tick(playing));
            this.Isolated("deployed", playing, static (core, playing) => core.deployed?.Tick(playing && core.Settings.DeployedCues));
            this.Isolated("traps", playing, static (core, playing) => core.traps?.Tick(playing && core.Settings.TrialTrapCues));
            this.Isolated("trial doors", playing, static (core, playing) => core.trialDoors?.Tick(playing && core.Settings.AnnounceTrialProgress));
            this.Isolated("enemy states", playing, static (core, playing) => core.enemyStates?.Tick(playing, core.Settings.EnemyStateCues, core.Settings.PrimedStunCues));
            this.Isolated("object sounds", playing, static (core, playing) => core.objectSounds?.Tick(playing && core.Settings.ObjectSounds));
            // A town is not explored; the guide, while it leads, is the one sound to follow.
            this.Isolated("lost hint", playing, static (core, playing) => core.lostHint?.Tick(
                playing && core.Settings.LostHint && core.beacon?.Target == null && !core.InTownOrHideout(),
                core.fights?.InFight(FightState.AnyEnemy) == true));
            this.Isolated("map hints", playing, static (core, playing) => core.mapHints?.Tick(playing && core.Settings.MapHints && !core.InTownOrHideout()));
            this.Isolated("discoveries", playing, static (core, playing) => core.discoveries?.Tick(playing && core.Settings.AnnounceDiscoveries));
            this.Isolated("enemy news", playing, static (core, playing) => core.enemyNews?.Tick(playing && core.Settings.AnnounceEnemies));
            this.Isolated("wall bump", playing, static (core, playing) =>
            {
                if (playing)
                {
                    core.wallBump?.Tick(core.padState.LeftX, core.padState.LeftY, core.Settings.WallBump);
                }
            });
        }

        /// <summary>The bosses' part of the frame: its settings, the trial's dodge scope, the tick, and what its field checks found.</summary>
        private void TickBosses(bool playing)
        {
            if (this.bosses == null)
            {
                return;
            }

            this.bosses.AnnounceLife = this.Settings.AnnounceBossLife;
            this.bosses.DodgeWarnings = this.Settings.DodgeWarnings;
            var now = Environment.TickCount64;
            this.trialScope.Saw(this.screen != null && this.narrator?.TrialShown(this.screen.Current) == true, now);
            var allMonsters = TrialDodgeScope.AllMonsters(this.Settings.DodgeWarningsAllMonsters, this.Settings.DodgeWarningsAllMonstersInTrials, this.trialScope.InTrial(now));
            if (allMonsters != this.bosses.AllMonsters)
            {
                this.log?.Info($"Dodge warnings for every monster: {(allMonsters ? "on" : "off")}{(this.trialScope.InTrial(now) ? " (a trial)" : string.Empty)}");
            }

            this.bosses.AllMonsters = allMonsters;
            this.bosses.ParryCues = this.Settings.ParryCues;
            this.bosses.UnblockableCues = this.Settings.UnblockableCues;
            this.bosses.CatalogDirectory = System.IO.Path.Join(this.DllDirectory, "catalog");
            this.bosses.Tick(playing);
            foreach (var (part, trust) in this.bosses.FieldNews())
            {
                // Each dodge field judged by its live check, said like any other part.
                if (trust == FieldTrust.Lost)
                {
                    this.compatibility?.Missing(part);
                }
                else
                {
                    this.compatibility?.Found(part);
                }
            }
        }

        /// <summary>The panel covering the game; none in local co-op, where it may be the other player's and this one's game goes on.</summary>
        private ScreenLayer? PanelOverGame() =>
            this.narrator == null || Seats.Other != null ? null : this.screen?.Current.PanelOver(this.narrator.HudLayer, this.narrator.TutorialLayer);

        /// <summary>
        ///     Nothing covers the game: no focus and no panel. The focus alone is not enough: a panel can hold none (a
        ///     confirmation box, the passive tree between nodes), and the world's sounds then played over it.
        /// </summary>
        private bool WorldInFront()
        {
            var panel = this.PanelOverGame();
            var address = panel?.Address ?? IntPtr.Zero;
            if (address != this.panelSeen)
            {
                this.panelSeen = address;
                this.log?.Info(panel == null ? "Panel over the game: none" : $"Panel over the game: layer {panel.Index}, {string.Join(" | ", panel.Texts.Take(PanelTextsLogged).Select(text => text.Text))}");
            }

            return this.worldGate.InFront(this.focus?.Current != null || panel != null, Environment.TickCount64);
        }

        /// <summary>
        ///     Runs one part of the frame; an exception is logged (throttled per part) and the frame goes on. The part is a
        ///     static lambda handed the plugin: about fifty run every frame, and a lambda that captured <c>this</c> or a local
        ///     made a delegate, and often a closure, for each of them each time.
        /// </summary>
        private void Isolated(string service, Action<Poe2AccessibilityCore> tick)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                tick(this);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault(service, ex);
            }

            this.NoteTickCost(service, start);
        }

        /// <summary>The same for a part that needs a fact of this frame (<paramref name="state"/>: playing, the game state), passed by value.</summary>
        private void Isolated<TState>(string service, TState state, Action<Poe2AccessibilityCore, TState> tick)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                tick(this, state);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault(service, ex);
            }

            this.NoteTickCost(service, start);
        }

        private void NoteTickCost(string service, long start)
        {
            var ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            if (this.tickCosts.Add(service, ticks, Environment.TickCount64))
            {
                this.log?.Info($"Slow tick: {service} took {ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0} ms");
            }
        }

        private void ReportTickFault(string service, Exception ex)
        {
            var now = Environment.TickCount64;
            if (!this.tickFaults.TryGetValue(service, out var last) || now - last >= SoundErrorReportMs)
            {
                this.tickFaults[service] = now;
                this.log?.Error($"Tick failed: {service}", ex);
            }
        }

        private static bool IsPlayerAlive() => IsAlive(Seats.Followed);

        private static bool IsAlive(GameReader.RemoteObjects.States.InGameStateObjects.Entity entity) =>
            entity.TryGetComponent<GameReader.RemoteObjects.Components.Life>(out var life) && life.IsAlive;

        /// <summary>Per frame in the game: each read player's own news (life, mana, level, states).</summary>
        private void TickPlayerNews()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState)
            {
                this.coopDowns.Reset(); // a loading screen (a respawn among them) is not a revival
                return;
            }

            // Each player's news and the co-op's downs on their own: one's watch failing must not silence the other's.
            var inWorld = this.WorldInFront() && Core.Process.Foreground;
            this.Isolated("own news", inWorld, static (core, inWorld) => core.alerts?.Tick(core.Settings, inWorld && IsPlayerAlive()));
            this.Isolated("partner news", inWorld, static (core, inWorld) =>
            {
                if (Seats.Partner is { } partner)
                {
                    core.partnerAlerts?.Tick(core.Settings, inWorld && IsAlive(partner));
                }
            });
            this.Isolated("co-op downs", static core =>
            {
                // Local co-op: who is down and who is back. A life not readable (entities reload with the area) is no news.
                var other = Seats.Other;
                if (LifeKnown(Seats.Followed) && (other == null || LifeKnown(other)))
                {
                    foreach (var news in core.coopDowns.Update(Core.States.InGameStateObject.CurrentAreaInstance.Address.ToInt64(), IsPlayerAlive(), other == null ? null : (PlayerAlerts.Name(other), IsAlive(other))))
                    {
                        core.output.Say(SpeechKind.LifeAndMana, news, true);
                    }
                }
            });
        }

        private static bool LifeKnown(GameReader.RemoteObjects.States.InGameStateObjects.Entity entity) =>
            entity.IsValid && entity.TryGetComponent<GameReader.RemoteObjects.Components.Life>(out var life) && life.Health.Total > 0;

        /// <summary>
        ///     The current area's level, which is its monsters' level (GameHelper AreaInstance +0xBC, "Monster Level" in its
        ///     overlay; the game's world map says "Monster level: 1" on the Riverbank); 0 when the read is not a level.
        /// </summary>
        private static int AreaLevel()
        {
            var level = Core.States.InGameStateObject.CurrentAreaInstance.CurrentAreaLevel;
            return level is > 0 and <= MaxAreaLevel ? level : 0;
        }

        /// <summary>The host's dormant-monster check reads <c>is_dead</c> by the key the game's own table gives it.</summary>
        private void HandDeadStat()
        {
            var key = (GameReader.RemoteEnums.GameStats)GameIds.Current.Stat(nameof(GameReader.RemoteEnums.GameStats.is_dead), (int)GameReader.RemoteEnums.GameStats.is_dead);
            if (key != GameReader.RemoteObjects.States.InGameStateObjects.Entity.DeadStat)
            {
                this.log?.Info($"Stat is_dead: the game says {(int)key}, the host's enum {(int)GameReader.RemoteObjects.States.InGameStateObjects.Entity.DeadStat}");
                GameReader.RemoteObjects.States.InGameStateObjects.Entity.DeadStat = key;
            }
        }

        /// <summary>"Life 80 percent, mana 40 percent" for the mod button; null outside gameplay.</summary>
        private string? ReadVitals()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState ||
                !Seats.Asking.TryGetComponent<GameReader.RemoteObjects.Components.Life>(out var life) ||
                life.Health.Total <= 0)
            {
                return null;
            }

            var vitals = Announcements.Vitals(
                life.Health.CurrentInPercent(),
                life.Mana.Total > 0 ? life.Mana.CurrentInPercent() : 0,
                life.EnergyShield.Total > 0 ? life.EnergyShield.CurrentInPercent() : null);
            var alerts = Seats.AskingSecond && Seats.Partner != null ? this.partnerAlerts : this.alerts;
            var states = alerts?.Statuses.Active.Select(Announcements.AilmentName).ToList() ?? new System.Collections.Generic.List<string>();
            var charges = string.Join(", ", (alerts?.Charges.Held.Select(held => Announcements.Charges(held.Kind, held.Count)) ?? Enumerable.Empty<string>())
                .Concat(alerts?.Charges.Rage is { Count: > 0 } rage ? new[] { Announcements.Rage(rage.Count, rage.Max) } : Array.Empty<string>())
                .Concat(alerts?.Resources.Held() ?? Enumerable.Empty<string>()));
            var player = Seats.Asking.TryGetComponent<GameReader.RemoteObjects.Components.Player>(out var me) ? me : null;
            var character = player?.Level ?? 0;
            var levels = Announcements.Levels(character, AreaLevel(), player == null ? null : this.experience?.PercentToNext(character, unchecked((uint)player.Xp)));
            var flasks = string.Join(", ", (Seats.AskingSecond && Seats.Partner != null ? this.partnerFlasks : this.flasks)?.Readout() ?? Enumerable.Empty<string>());
            var explored = this.entities?.ExploredPercent() is { } percent ? Announcements.Explored(percent) : string.Empty;
            var gold = this.gold?.Amount is { } coins ? Announcements.Amount(coins.ToString(System.Globalization.CultureInfo.CurrentCulture), Announcements.Gold, false) : string.Empty;
            var encounter = Announcements.Sentences((this.encounters?.Lines(this.narrator?.HudLayer ?? IntPtr.Zero) ?? Array.Empty<string>())
                .Prepend(this.screen is { } watcher ? this.narrator?.TrialStatus(watcher.Current) ?? string.Empty : string.Empty));
            var minions = this.minions == null ? string.Empty : MinionGroups.Describe(this.minions.Near());
            return Announcements.Sentences(new[] { vitals, string.Join(", ", states), charges, minions, this.bosses?.Status() ?? string.Empty, flasks, levels, gold, encounter, explored }.Where(part => part.Length > 0));
        }

        /// <summary>The delivery level the player chose for a kind of news (speech priority).</summary>
        private string SpeechLevel(SpeechKind kind) => kind switch
        {
            SpeechKind.StatusBegins => this.Settings.SpeechStatusBegins,
            SpeechKind.StatusEnds => this.Settings.SpeechStatusEnds,
            SpeechKind.LifeAndMana => this.Settings.SpeechLifeAndMana,
            SpeechKind.BossLife => this.Settings.SpeechBossLife,
            SpeechKind.EnemyNames => this.Settings.SpeechEnemyNames,
            SpeechKind.Quest => this.Settings.SpeechQuest,
            SpeechKind.Places => this.Settings.SpeechPlaces,
            SpeechKind.GroundItems => this.Settings.SpeechGroundItems,
            SpeechKind.Charges => this.Settings.SpeechCharges,
            SpeechKind.Combo => this.Settings.SpeechCombo,
            SpeechKind.Cooldowns => this.Settings.SpeechCooldowns,
            _ => this.Settings.SpeechArrivals,
        };

        /// <summary>The skill bar's sets in screen order (D2R skill bar readout); null outside gameplay or when not found.</summary>
        private string? ReadSkillBar()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState || this.semantics == null || this.focus == null)
            {
                return null;
            }

            var sets = this.semantics.ReadSkillBar(this.focus.LayerContainer, Seats.AskingSecond);
            return sets.Count == 0 ? null : Announcements.Sentences(sets.Select(set => Announcements.SkillSet(set.Title, set.Bindings)));
        }

        /// <summary>The HUD's texts (quest tracker among them) for the scanner's Quest category.</summary>
        /// <summary>The shop page is looked for this often while nothing has the focus (its search walks panels_parent).</summary>
        private const int ShopLookupMs = 250;

        private long shopLookedUpAt = long.MinValue / 2;
        private IntPtr shopPage;

        /// <summary>The shop's page while it is shown (<see cref="UiSemantics.ShopPage"/>), looked up at most every ShopLookupMs.</summary>
        private IntPtr ShopPage()
        {
            var now = Environment.TickCount64;
            if (now - this.shopLookedUpAt >= ShopLookupMs)
            {
                this.shopLookedUpAt = now;
                this.shopPage = this.semantics?.ShopPage(this.locator?.Fields.Find(this.focus?.LayerContainer ?? IntPtr.Zero, InGameField.InventoryPanel) ?? IntPtr.Zero) ?? IntPtr.Zero;
            }

            return this.shopPage;
        }

        /// <summary>Where the shop's chosen card shows: its open item page, else the whole page; looked up as often as the page.</summary>
        private IntPtr ShopHighlightRoot()
        {
            var page = this.ShopPage();
            if (page == IntPtr.Zero || Environment.TickCount64 - this.shopRootLookedUpAt < ShopLookupMs)
            {
                return page == IntPtr.Zero ? page : this.shopRoot;
            }

            this.shopRootLookedUpAt = Environment.TickCount64;
            return this.shopRoot = this.semantics?.ShopHighlightRoot(page) ?? page;
        }

        private long shopRootLookedUpAt = long.MinValue / 2;
        private IntPtr shopRoot;

        private long worldScreenLookedUpAt = long.MinValue / 2;
        private IntPtr worldScreen;

        /// <summary>The world map while it is shown (<see cref="UiSemantics.WorldScreen"/>), looked up at most every ShopLookupMs.</summary>
        private IntPtr WorldScreen()
        {
            var now = Environment.TickCount64;
            if (now - this.worldScreenLookedUpAt >= ShopLookupMs)
            {
                this.worldScreenLookedUpAt = now;
                this.worldScreen = this.semantics?.WorldScreen(this.locator?.Fields.Find(this.focus?.LayerContainer ?? IntPtr.Zero, InGameField.InventoryPanel) ?? IntPtr.Zero) ?? IntPtr.Zero;
            }

            return this.worldScreen;
        }

        /// <summary>A moment for the player's place to be read in a new instance.</summary>
        private const int PlayerSettlesMs = 300;

        /// <summary>" Bottom left corner." after the area's name, where the player starts on the map; not in towns and hideouts.</summary>
        private string AreaCornerLine()
        {
            if (!this.Settings.AreaCorner || InTownOrHideout() || !Seats.Followed.TryGetComponent<GameReader.RemoteObjects.Components.Render>(out var player))
            {
                return string.Empty;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var (alongX, alongY) = ScreenProjection.Axes(player.WorldPosition);
            var cell = new System.Numerics.Vector2(player.GridPosition.X, player.GridPosition.Y);
            return MapCorner.Of(area.GridWalkableData, area.TerrainMetadata.BytesPerRow, cell, alongX, alongY) is { } corner
                ? $" {Announcements.CornerOfMap(corner.Across, corner.Down)}."
                : string.Empty;
        }

        /// <summary>From the area row the loading state holds; the host's WorldData row only before that row was read.</summary>
        private bool InTownOrHideout() =>
            this.gameState is { CurrentAreaId.Length: > 0 } state ? state.InTownOrHideout
                : Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails is { } area && (area.IsTown || area.IsHideout);

        /// <summary>Puts the guide on the nearest Death Crystal, and on the next once that one is disabled.</summary>
        private void GuideToTimed(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || !this.Settings.GuideTimed || this.entities == null || now < this.nextTimedGuide)
            {
                return;
            }

            this.nextTimedGuide = now + TimedGuideMs;
            var timed = this.entities.Entries().Where(entry => entry.Timed && entry.Category != Services.ScanCategory.Quest &&
                                                               this.entities.DistanceTo(entry) <= TimedRoomWorld).ToList();
            var (next, release) = this.timedGuide.Look(timed.Select(entry => entry.Id).ToList(), this.beacon!.Target?.Id);
            if (next != null)
            {
                // The quickest to reach, not the nearest through a wall (user 2026-11-07); with no way known, the nearest.
                var quickest = timed.Take(MaxTimedRoutes).OrderBy(this.entities.WalkTo).First();
                this.timedGuide.Aim(quickest.Id);
                this.beacon.Lock(quickest);
            }
            else if (release)
            {
                this.beacon.Clear(false);
            }
        }

        /// <summary>
        ///     What the quest tracker asks for. A quest's name is not an instruction: "El pozo de las almas" made every town's
        ///     well the objective (docs/research/ui-layers.md, quest tracker).
        /// </summary>
        private System.Collections.Generic.IReadOnlyList<string> QuestTexts()
        {
            var hud = this.narrator == null ? null : this.screen?.Current.Find(this.narrator.HudLayer);
            return hud?.Texts.Where(text => text.Name != QuestTitleLabel).Select(text => text.Text).ToList() ?? (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>The tracker's label for a quest's name; its objectives are "quest_info" (UI dump 2026-11-10).</summary>
        private const string QuestTitleLabel = "title_label";
    }
}
