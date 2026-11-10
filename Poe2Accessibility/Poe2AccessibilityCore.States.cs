namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using GameReader.Plugin;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Game state and area changes of <see cref="Poe2AccessibilityCore"/>: what is said when the game attaches, changes state, closes or loads an area.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>When the game went to "not loaded"; zero once announced or left (see <see cref="AnnounceLastingClose"/>).</summary>
        private long closedSince;

        /// <summary>A "not loaded" state shorter than this is a transition, not the game closing.</summary>
        private const int ClosedSettleMs = 2000;

        /// <summary>
        ///     A state the game may only pass through is said once it has lasted this long: entering the game goes
        ///     through character selection for 18 ms ("Cargando. Selección de personaje. Cargando área", 2026-10-24).
        ///     The others are said at once, before the area name or the menu they bring.
        /// </summary>
        private const int StateSettleMs = 150;

        private static readonly System.Collections.Generic.HashSet<GameStateTypes> PassingStates = new()
        {
            GameStateTypes.SelectCharacterState, GameStateTypes.WaitingState, GameStateTypes.LoadingState,
        };

        /// <summary>The state announcement waiting out <see cref="StateSettleMs"/>; Detected keeps "Game detected" when replaced.</summary>
        private (string Text, bool Detected, long At, bool Queued)? pendingState;

        /// <summary>
        ///     "Area: X, level N" waiting for the instance it names: the area row can change before the game loads the new
        ///     instance (a new Clearfell from the instance list said the town's level 15, 2026-11-02).
        /// </summary>
        private (string Name, bool Resumed, long At)? pendingArea;

        /// <summary>An area announcement waits at most this long for its instance, then says what it has.</summary>
        private const long AreaInstanceWaitMs = 15000;

        private void OnAttachedChanged(bool attached)
        {
            this.processProbe?.Reset();
            if (attached)
            {
                this.NoteGameBuild();

                // The game asks for a restart to change its language: a game started again under a running host speaks
                // the new one (a fresh install starts in English; Español applies on the next start, 2026-10-24).
                Localization.Detect(this.Settings.Language, message => this.log?.Info(message));
            }

            // With state announcements on, "Game detected" and the screen follow at once and would cut the attach line off.
            if (!attached || !this.Settings.AnnounceGameState)
            {
                this.output.Speak(attached ? Announcements.GameAttached : Announcements.GameNotAttached, false);
            }
        }

        private void OnGameStateChanged(GameStateTypes previous, GameStateTypes current)
        {
            this.research?.OnStateChanged(current);
            if (current == GameStateTypes.SelectCharacterState)
            {
                this.creationHelpSaid = false;
                this.classHelpSaid = false;
            }

            if (current == GameStateTypes.AreaLoadingState)
            {
                // A respawn reloads the same area without an area change; its barriers come back reading open for a
                // moment ("Se abrió el paso" after a death, 2026-10-24): learnt again without a word.
                this.barriers?.Reset();
            }

            this.screen?.Reset(false);
            this.narrator?.Reset(current == GameStateTypes.InGameState);
            if (this.narrator != null)
            {
                // The game menu's screen carries the pause banner and a loading tip: its focus alone opens it.
                this.narrator.ReadScreenMessages = current != GameStateTypes.EscapeState;
                this.narrator.LoadingArea = current == GameStateTypes.AreaLoadingState;
                this.narrator.SaidTitle = current == GameStateTypes.EscapeState ? Announcements.ForState(current) : string.Empty;
            }
            if (this.semantics != null)
            {
                this.semantics.InGame = current == GameStateTypes.InGameState;
            }

            if (previous == GameStateTypes.EscapeState && current == GameStateTypes.InGameState)
            {
                // Back to play from the game menu: its long pause banner stops (user 2026-10-28). Options opened from
                // the menu are read after this, as their page arrives.
                this.output.Stop();
            }

            if (!this.Settings.AnnounceGameState)
            {
                return;
            }

            if (current == GameStateTypes.GameNotLoaded)
            {
                // Leaving the game for character selection passes through "not loaded" for a moment: only a state
                // that lasts is the game closing.
                this.closedSince = Environment.TickCount64;
                return;
            }

            if (previous == GameStateTypes.EscapeState && current == GameStateTypes.InGameState)
            {
                return; // the game menu closed (D2R: closing is not news), or Options opened from it: its page speaks
            }

            var reallyClosed = previous == GameStateTypes.GameNotLoaded && this.closedSince == 0;
            this.closedSince = 0;
            if (previous == GameStateTypes.AreaLoadingState && current == GameStateTypes.InGameState && this.pendingState?.Detected != true)
            {
                // A new area's name follows at once and replaces it ("En juego" before every area name was noise,
                // 2026-10-25); a respawn in the same area keeps it as the end of the load.
                this.pendingState = (Announcements.ForState(current), false, Environment.TickCount64, false);
                return;
            }

            // Taking an exit, the game's own "Entrando a Sierraclara..." was cut off by "Cargando área" (2026-10-25).
            var queued = previous == GameStateTypes.InGameState && current == GameStateTypes.AreaLoadingState;
            this.pendingState = (Announcements.ForState(current), reallyClosed || this.pendingState?.Detected == true, Environment.TickCount64, queued);
            if (!PassingStates.Contains(current))
            {
                this.pendingState = this.pendingState.Value with { At = long.MinValue / 2 };
                this.AnnounceSettledState();
            }

            this.SayCreationClassHelp(current);
        }

        /// <summary>Per frame: the state reached, once it has settled; a passing one is replaced unsaid.</summary>
        private void AnnounceSettledState()
        {
            if (this.pendingState is not { } pending || Environment.TickCount64 - pending.At < StateSettleMs)
            {
                return;
            }

            this.pendingState = null;
            // Held: the screen's first focus cut "Menú del juego." and "Selección de personaje." 300 ms in (2026-10-26).
            var text = pending.Detected ? $"{Announcements.GameDetected} {pending.Text}" : pending.Text;
            this.output.Keep(text, SpeechFloor.MessageHoldMs(text), !pending.Queued, yieldsToMoves: true);
        }

        /// <summary>Per frame: "Game closed" once the game has stayed unloaded for a while.</summary>
        private void AnnounceLastingClose()
        {
            if (this.closedSince != 0 && Environment.TickCount64 - this.closedSince >= ClosedSettleMs)
            {
                this.closedSince = 0;
                if (this.Settings.AnnounceGameState)
                {
                    this.output.Speak(Announcements.ForState(GameStateTypes.GameNotLoaded), true);
                }
            }
        }

        private void OnAreaChanged(string areaName, bool resumed)
        {
            this.beacon?.Clear(false);
            this.bosses?.Reset();
            this.trialScope.Reset();
            this.weakPoint?.Reset();
            this.shield?.Reset();
            this.culls?.Reset();
            this.deployed?.Reset();
            this.traps?.Reset();
            this.trialDoors?.Reset();
            this.enemyStates?.Reset();
            this.alerts?.Statuses.Reset();
            this.partnerAlerts?.Statuses.Reset();
            this.alerts?.Charges.Reset();
            this.partnerAlerts?.Charges.Reset();
            this.alerts?.Resources.Reset();
            this.partnerAlerts?.Resources.Reset();
            this.alerts?.Cooldowns.Reset();
            this.partnerAlerts?.Cooldowns.Reset();
            this.pickups?.Reset();
            this.partnerPickups?.Reset();
            this.gold?.Reset();
            this.flasks?.Reset();
            this.partnerFlasks?.Reset();
            this.coopDowns.Reset();
            this.barriers?.Reset();
            this.targets?.Reset();
            this.discoveries?.Reset(areaName, resumed);
            this.enemyNews?.Reset();
            this.entities?.AreaEntered(areaName);
            this.lostHint?.Reset();
            this.nearby?.Reset();
            this.places?.Reset();
            this.narrator?.NewArea(resumed);
            if (this.Settings.AnnounceArea)
            {
                if (this.pendingState is { Detected: false } loaded && loaded.Text == Announcements.ForState(GameStateTypes.InGameState))
                {
                    this.pendingState = null;
                }

                this.pendingArea = (areaName, resumed, Environment.TickCount64);
                this.areaReadyAt = null;
                this.AnnounceLoadedArea();
            }
        }

        /// <summary>The tile table of the instance the last area announcement read: GameHelper makes a new one each load.</summary>
        private object? areaTilesSaid;

        /// <summary>When the instance to announce was first there; the corner waits for the player's place in it.</summary>
        private long? areaReadyAt;

        /// <summary>Per frame: the pending area announcement once the game has loaded an instance since the last one said.</summary>
        private void AnnounceLoadedArea()
        {
            if (this.pendingArea is not var (name, resumed, at))
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var tiles = area.TgtTilesLocations;
            if (!resumed && (area.AreaHash.Length == 0 || ReferenceEquals(tiles, this.areaTilesSaid)) && Environment.TickCount64 - at < AreaInstanceWaitMs)
            {
                return;
            }

            var now = Environment.TickCount64;
            this.areaReadyAt ??= now;
            if (this.Settings.AreaCorner && now - this.areaReadyAt < PlayerSettlesMs)
            {
                return; // the line waits behind "Loading area" anyway; the player's place may still be the last area's
            }

            this.pendingArea = null;
            this.areaTilesSaid = tiles;
            var level = AreaLevel();
            this.log?.Info($"Area level {level} (instance {area.AreaHash})");
            this.output.Say(SpeechKind.Places, Announcements.Area(name, level) + this.AreaCornerLine());
        }
    }
}
