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
                    this.enemyCues.Tick(playing && (this.Settings.EnemySounds || this.Settings.NpcSounds || this.Settings.MeleeReach));
                }
            });
            this.Isolated("radar", () => this.radar?.Tick(playing && this.Settings.Radar));
            this.Isolated("partner radar", () => this.partnerRadar?.Tick(
                inWorld && this.Settings.PartnerRadar && this.localPlayers?.Second() is { } partner && IsAlive(partner)));
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
            this.Isolated("states", () => this.statuses?.Tick(playing && this.Settings.StatusAlerts));
            if (this.gameState?.CurrentState == GameStateTypes.InGameState)
            {
                this.Isolated("pick-ups", () => this.pickups?.Tick(playing && this.Settings.AnnouncePickups));
                this.Isolated("hand", () => this.pickups?.TickHand(this.Settings.AnnounceHand));
                this.Isolated("moves", () => this.pickups?.TickMoves(playing, this.Settings.AnnounceMoves));
                this.Isolated("gold", () => this.gold?.Tick(this.focus?.LayerContainer ?? IntPtr.Zero, playing, this.Settings.AnnouncePickups));
                this.Isolated("flasks", () => this.flasks?.Tick(playing, this.Settings.AnnounceFlasks));
                this.Isolated("barriers", () => this.barriers?.Tick(this.Settings.AnnounceDiscoveries));
            }

            this.Isolated("bosses", () =>
            {
                if (this.bosses != null)
                {
                    this.bosses.AnnounceLife = this.Settings.AnnounceBossLife;
                    this.bosses.DodgeWarnings = this.Settings.DodgeWarnings;
                    this.bosses.Tick(playing);
                }
            });
            this.Isolated("object sounds", () => this.objectSounds?.Tick(playing && this.Settings.ObjectSounds));
            this.Isolated("discoveries", () => this.discoveries?.Tick(playing && this.Settings.AnnounceDiscoveries));
            this.Isolated("enemy news", () => this.enemyNews?.Tick(playing && this.Settings.AnnounceEnemies));
            this.Isolated("wall bump", () =>
            {
                if (playing)
                {
                    this.wallBump?.Tick(this.padState.LeftX, this.padState.LeftY, this.Settings.WallBump);
                }
            });
            this.Isolated("level", this.WatchLevel);
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

        /// <summary>"Level 3" when the character levels up; the first reading of a character is only remembered.</summary>
        private void WatchLevel()
        {
            var player = Core.States.InGameStateObject.CurrentAreaInstance.Player;
            if (!player.TryGetComponent<GameHelper.RemoteObjects.Components.Player>(out var character) || character.Level <= 0)
            {
                return;
            }

            if (this.lastLevel.Character == character.Name && character.Level > this.lastLevel.Level && this.Settings.AnnounceLevelUp)
            {
                this.output.Speak(Announcements.LevelUp(character.Level), false);
            }

            this.lastLevel = (character.Name, character.Level);
        }

        private static bool IsPlayerAlive() => IsAlive(Core.States.InGameStateObject.CurrentAreaInstance.Player);

        private static bool IsAlive(GameHelper.RemoteObjects.States.InGameStateObjects.Entity entity) =>
            entity.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) && life.IsAlive;

        /// <summary>Per frame: a warning when the player's life falls below a threshold.</summary>
        private void WatchLife()
        {
            if (!this.Settings.AnnounceLowLife || this.gameState?.CurrentState != GameStateTypes.InGameState ||
                !Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) ||
                !life.IsAlive || life.Health.Total <= 0)
            {
                return;
            }

            if (this.lifeAlarm.Update(life.Health.CurrentInPercent()) is { } threshold)
            {
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowLife(threshold), threshold <= this.Settings.LowLifeBelow);
            }

            if (this.Settings.AnnounceLowMana && life.Mana.Total > 0 && this.manaAlarm.Update(life.Mana.CurrentInPercent()) is { } manaThreshold)
            {
                // Mana runs low in every fight: urgent only when it is nearly gone.
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowMana(manaThreshold), manaThreshold <= 10);
            }
        }

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
                !Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Life>(out var life) ||
                life.Health.Total <= 0)
            {
                return null;
            }

            var vitals = Announcements.Vitals(
                life.Health.CurrentInPercent(),
                life.Mana.Total > 0 ? life.Mana.CurrentInPercent() : 0,
                life.EnergyShield.Total > 0 ? life.EnergyShield.CurrentInPercent() : null);
            var states = this.statuses?.Active.Select(Announcements.AilmentName).ToList() ?? new System.Collections.Generic.List<string>();
            var player = Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<GameHelper.RemoteObjects.Components.Player>(out var me) ? me : null;
            var character = player?.Level ?? 0;
            var levels = Announcements.Levels(character, AreaLevel(), player == null ? null : this.experience?.PercentToNext(character, unchecked((uint)player.Xp)));
            var flasks = string.Join(", ", this.flasks?.Flasks().Select(flask => Announcements.Flask(flask.Name, flask.Uses)) ?? Enumerable.Empty<string>());
            var explored = this.entities?.ExploredPercent() is { } percent ? Announcements.Explored(percent) : string.Empty;
            var gold = this.gold?.Amount is { } coins ? Announcements.Amount(coins.ToString(System.Globalization.CultureInfo.CurrentCulture), Announcements.Gold, false) : string.Empty;
            return Announcements.Sentences(new[] { vitals, string.Join(", ", states), this.bosses?.Status() ?? string.Empty, flasks, levels, gold, explored }.Where(part => part.Length > 0));
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

            var sets = this.semantics.ReadSkillBar(this.focus.LayerContainer);
            return sets.Count == 0 ? null : Announcements.Sentences(sets.Select(set => Announcements.SkillSet(set.Title, set.Bindings)));
        }

        /// <summary>The HUD's texts (quest tracker among them) for the scanner's Quest category.</summary>
        private System.Collections.Generic.IReadOnlyList<string> QuestTexts()
        {
            var hud = this.narrator == null ? null : this.screen?.Current.Find(this.narrator.HudLayer);
            return hud?.Texts.Select(text => text.Text).ToList() ?? (System.Collections.Generic.IReadOnlyList<string>)Array.Empty<string>();
        }
    }
}
