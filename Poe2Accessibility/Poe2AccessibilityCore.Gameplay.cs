namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Services;

    /// <summary>Gameplay ticks, warnings and readouts of <see cref="Poe2AccessibilityCore"/>, each service isolated from the others' faults.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>
        ///     Per frame: radar, enemy sounds and what is nearby, only in gameplay. Each service runs on its own (D2R
        ///     crash containment): one that throws is logged (throttled per service) and the others still run.
        /// </summary>
        private void TickSounds()
        {
            bool inWorld, playing;
            try
            {
                inWorld = Core.Process.Foreground && this.focus?.Current == null && this.gameState?.CurrentState == GameStateTypes.InGameState;
                playing = inWorld && IsPlayerAlive();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault("gameplay state", ex);
                return;
            }

            this.Isolated("enemy sounds", () =>
            {
                if (this.enemyCues != null)
                {
                    this.enemyCues.Enemies = this.Settings.EnemySounds;
                    this.enemyCues.Friends = this.Settings.NpcSounds;
                    this.enemyCues.Players = this.Settings.PlayerSounds;
                    this.enemyCues.Tick(playing && (this.Settings.EnemySounds || this.Settings.NpcSounds || this.Settings.MeleeReach));
                }
            });
            this.Isolated("radar", () => this.radar?.Tick(playing && this.Settings.Radar));
            this.Isolated("partner radar", () => this.partnerRadar?.Tick(
                inWorld && this.Settings.Radar && Seats.Partner is { } partner && IsAlive(partner)));
            this.Isolated("wall tones", () => this.wallTones?.Tick(playing && this.Settings.WallTones));
            this.Isolated("nearby", () => this.nearby?.Tick(playing, this.Settings.AnnounceNearby, this.Settings.AnnounceInteractTarget));
            this.Isolated("places", () => this.places?.Tick(playing, this.Settings.AnnouncePlaces));
            this.Isolated("exploration", () =>
            {
                if (playing)
                {
                    this.entities?.Track();
                }
            });
            this.Isolated("beacon", () =>
            {
                if (this.beacon != null)
                {
                    this.beacon.TurnPrompts = this.Settings.RoutePrompts;
                    this.beacon.Tick(playing);
                }
            });
            this.Isolated("target", () =>
            {
                var aiming = !this.padState.IsDown(GameLayout.PadButtons.LeftStick) && !(this.commands?.InSettingsMenu ?? false) &&
                             MathF.Max(MathF.Abs(this.padState.RightX), MathF.Abs(this.padState.RightY)) >= AimDeflection;
                if (this.targets != null)
                {
                    this.targets.Names = this.Settings.AnnounceTarget;
                    this.targets.ModNames = this.Settings.AnnounceMonsterMods;
                    this.targets.Beep = this.Settings.CrosshairBeep;
                    this.targets.Tick(playing && (this.Settings.AnnounceTarget || this.Settings.CrosshairBeep), aiming);
                }
            });
            if (this.gameState?.CurrentState == GameStateTypes.InGameState)
            {
                this.Isolated("pick-ups", () => this.pickups?.Tick(playing && this.Settings.AnnouncePickups));
                this.Isolated("hand", () => this.pickups?.TickHand(this.Settings.AnnounceHand));
                this.Isolated("moves", () => this.pickups?.TickMoves(playing, this.Settings.AnnounceMoves));
                this.Isolated("gold", () => this.gold?.Tick(this.focus?.LayerContainer ?? IntPtr.Zero, playing, this.Settings.AnnouncePickups));
                this.Isolated("flasks", () => this.flasks?.Tick(playing, this.Settings.AnnounceFlasks));
                if (Seats.Partner is { } partner)
                {
                    // The partner's own pick-ups, hand, moves and flasks, named (its menus are read for whoever moves in them).
                    var partnerPlaying = inWorld && IsAlive(partner);
                    this.Isolated("partner pick-ups", () => this.partnerPickups?.Tick(partnerPlaying && this.Settings.AnnouncePickups));
                    this.Isolated("partner hand", () => this.partnerPickups?.TickHand(this.Settings.AnnounceHand));
                    this.Isolated("partner moves", () => this.partnerPickups?.TickMoves(partnerPlaying, this.Settings.AnnounceMoves));
                    this.Isolated("partner flasks", () => this.partnerFlasks?.Tick(partnerPlaying, this.Settings.AnnounceFlasks));
                }
                this.Isolated("barriers", () => this.barriers?.Tick(this.Settings.AnnounceDiscoveries));
            }

            this.Isolated("bosses", () =>
            {
                if (this.bosses != null)
                {
                    this.bosses.AnnounceLife = this.Settings.AnnounceBossLife;
                    this.bosses.DodgeWarnings = this.Settings.DodgeWarnings;
                    this.bosses.ParryCues = this.Settings.ParryCues;
                    this.bosses.UnblockableCues = this.Settings.UnblockableCues;
                    this.bosses.Tick(playing);
                }
            });
            this.Isolated("weak point", () => this.weakPoint?.Tick(playing && this.Settings.BossWeakPoint));
            this.Isolated("hazards", () => this.hazards?.Tick(playing && this.Settings.HazardAlarm));
            this.Isolated("shield", () => this.shield?.Tick(playing, this.Settings.ShieldSounds, this.Settings.AnnounceStunBar));
            this.Isolated("cull", () => this.culls?.Tick(playing));
            this.Isolated("enemy states", () => this.enemyStates?.Tick(playing, this.Settings.EnemyStateCues, this.Settings.PrimedStunCues));
            this.Isolated("object sounds", () => this.objectSounds?.Tick(playing && this.Settings.ObjectSounds));
            // A town is not explored; the guide, while it leads, is the one sound to follow.
            this.Isolated("unexplored loop", () => this.unexploredLoop?.Tick(
                playing && this.Settings.UnexploredLoop && this.beacon?.Target == null && !InTownOrHideout()));
            this.Isolated("discoveries", () => this.discoveries?.Tick(playing && this.Settings.AnnounceDiscoveries));
            this.Isolated("enemy news", () => this.enemyNews?.Tick(playing && this.Settings.AnnounceEnemies));
            this.Isolated("wall bump", () =>
            {
                if (playing)
                {
                    this.wallBump?.Tick(this.padState.LeftX, this.padState.LeftY, this.Settings.WallBump);
                }
            });
        }

        /// <summary>Runs one gameplay service; an exception is logged (throttled per service) and the frame goes on.</summary>
        private void Isolated(string service, Action tick)
        {
            try
            {
                tick();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.ReportTickFault(service, ex);
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

        private static bool IsAlive(GameHelper.RemoteObjects.States.InGameStateObjects.Entity entity) =>
            entity.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) && life.IsAlive;

        /// <summary>Per frame in the game: each read player's own news (life, mana, level, states).</summary>
        private void TickPlayerNews()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState)
            {
                this.coopDowns.Reset(); // a loading screen (a respawn among them) is not a revival
                return;
            }

            var inWorld = Core.Process.Foreground && this.focus?.Current == null;
            this.alerts?.Tick(this.Settings, inWorld && IsPlayerAlive());
            if (Seats.Partner is { } partner)
            {
                this.partnerAlerts?.Tick(this.Settings, inWorld && IsAlive(partner));
            }

            // Local co-op: who is down and who is back. A life not readable (entities reload with the area) is no news.
            var other = Seats.Other;
            if (LifeKnown(Seats.Followed) && (other == null || LifeKnown(other)))
            {
                foreach (var news in this.coopDowns.Update(Core.States.InGameStateObject.CurrentAreaInstance.Address.ToInt64(), IsPlayerAlive(), other == null ? null : (PlayerAlerts.Name(other), IsAlive(other))))
                {
                    this.output.Say(SpeechKind.LifeAndMana, news, true);
                }
            }
        }

        private static bool LifeKnown(GameHelper.RemoteObjects.States.InGameStateObjects.Entity entity) =>
            entity.IsValid && entity.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) && life.Health.Total > 0;

        /// <summary>
        ///     The current area's level, which is its monsters' level (GameHelper AreaInstance +0xBC, "Monster Level" in its
        ///     overlay; the game's world map says "Monster level: 1" on the Riverbank); 0 when the read is not a level.
        /// </summary>
        private static int AreaLevel()
        {
            var level = Core.States.InGameStateObject.CurrentAreaInstance.CurrentAreaLevel;
            return level is > 0 and <= MaxAreaLevel ? level : 0;
        }

        /// <summary>"Life 80 percent, mana 40 percent" for the mod button; null outside gameplay.</summary>
        private string? ReadVitals()
        {
            if (this.gameState?.CurrentState != GameStateTypes.InGameState ||
                !Seats.Asking.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) ||
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
                .Concat(alerts?.Charges.Rage is { Count: > 0 } rage ? new[] { Announcements.Rage(rage.Count, rage.Max) } : Array.Empty<string>()));
            var player = Seats.Asking.TryGetComponent<GameHelper.RemoteObjects.Components.Player>(out var me) ? me : null;
            var character = player?.Level ?? 0;
            var levels = Announcements.Levels(character, AreaLevel(), player == null ? null : this.experience?.PercentToNext(character, unchecked((uint)player.Xp)));
            var flasks = string.Join(", ", (Seats.AskingSecond && Seats.Partner != null ? this.partnerFlasks : this.flasks)?.Flasks().Select(flask => Announcements.Flask(flask.Name, flask.Uses)) ?? Enumerable.Empty<string>());
            var explored = this.entities?.ExploredPercent() is { } percent ? Announcements.Explored(percent) : string.Empty;
            var gold = this.gold?.Amount is { } coins ? Announcements.Amount(coins.ToString(System.Globalization.CultureInfo.CurrentCulture), Announcements.Gold, false) : string.Empty;
            var encounter = Announcements.Sentences(this.encounters?.Lines(this.narrator?.HudLayer ?? IntPtr.Zero) ?? Array.Empty<string>());
            return Announcements.Sentences(new[] { vitals, string.Join(", ", states), charges, this.bosses?.Status() ?? string.Empty, flasks, levels, gold, encounter, explored }.Where(part => part.Length > 0));
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

        private static bool InTownOrHideout() =>
            Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails is { } area && (area.IsTown || area.IsHideout);

        private System.Collections.Generic.IReadOnlyList<string> QuestTexts()
        {
            var hud = this.narrator == null ? null : this.screen?.Current.Find(this.narrator.HudLayer);
            return hud?.Texts.Select(text => text.Text).ToList() ?? (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>();
        }
    }
}
