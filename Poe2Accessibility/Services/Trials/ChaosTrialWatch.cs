namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Says what a sighted player sees of a Trial of Chaos round: which kind of round its pieces make, a blood circle
    ///     filling, the core taken in hand, and the Ruin counted over the head (docs/research/trial-of-chaos.md).
    /// </summary>
    public sealed class ChaosTrialWatch
    {
        /// <summary>A lift's lever opens for half a second as it arrives: polled often enough to see it.</summary>
        private const int PollMs = 200;

        private const float RiderWorld = ChaosTrial.LiftZoneWorld;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly ChaosRoundNews news = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly List<(uint Id, ChaosPiece Piece)> pieces = new();
        private readonly ExitLeverNews exitLever = new();
        private readonly List<(uint Id, bool Enabled, float Distance)> levers = new();
        private readonly Dictionary<uint, ScanEntry> leverEntries = new();
        private readonly Dictionary<uint, bool> leverWas = new();
        private readonly EntityCatalog catalog;
        private readonly HostMemoryReader memory;
        private readonly LiftNews lifts = new();
        private long lastPoll;

        public ChaosTrialWatch(EntityCatalog catalog, HostMemoryReader memory, IAccessibilityOutputService output, PluginLog log)
        {
            this.catalog = catalog;
            this.memory = memory;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: nothing is known of its rounds.</summary>
        public void Reset()
        {
            this.news.Reset();
            this.exitLever.Reset();
            this.leverWas.Clear();
            this.lifts.Reset();
        }

        /// <summary>Gets or sets what sounds a lift that just turned ridable, from where it stands (the way-opened sound).</summary>
        public Action<ScanEntry>? LiftReady { get; set; }

        /// <summary>A room was completed: the lever that takes the player on is said once the way opens.</summary>
        public void RoundWon() => this.exitLever.RoundWon(Environment.TickCount64);

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            this.pieces.Clear();
            this.levers.Clear();
            this.leverEntries.Clear();

            System.Numerics.Vector3? player = Seats.Followed.TryGetComponent<Render>(out var seat) ? new(seat.WorldPosition.X, seat.WorldPosition.Y, seat.WorldPosition.Z) : null;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || ChaosTrial.PieceOf(entity.Path ?? string.Empty) is not { } piece)
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                this.LogOnce(entity.Path!, states);
                this.pieces.Add((entity.Id, piece));
                if (piece == ChaosPiece.LiftLever && player is { } from && entity.TryGetComponent<Render>(out var render))
                {
                    var at = render.WorldPosition;
                    var enabled = states.Count > 0 && ChaosTrial.IsEnabled(states);
                    if (this.leverWas.TryGetValue(entity.Id, out var was) && was != enabled)
                    {
                        this.log.Info($"Lever {entity.Id} {(enabled ? "opened" : "shut")}");
                    }

                    this.leverWas[entity.Id] = enabled;
                    this.levers.Add((entity.Id, enabled, System.Numerics.Vector3.Distance(from, new(at.X, at.Y, at.Z))));
                    this.leverEntries[entity.Id] = new ScanEntry(entity.Id, ScanCategory.Quest, Announcements.ChaosExitLever, at.X, at.Y, at.Z);
                }

                if (piece == ChaosPiece.Lift && player is { } rider && entity.TryGetComponent<Render>(out var platform))
                {
                    // Said to the one riding it: within the lift's own zone (ControlZone radius 29 cells, LiftPlatformLarge.ot), on the
                    // ground's plane: the platform's height is far from the player's while it is down (live 2026-11-19, "bajando" lost).
                    var far = System.Numerics.Vector2.Distance(new(rider.X, rider.Y), new(platform.WorldPosition.X, platform.WorldPosition.Y));
                    if (this.lifts.Moved(entity.Id, TransitionState.Of(this.memory, entity)?.State, now) is { } moved)
                    {
                        this.log.Info($"Lift {entity.Id} set off to transition {moved}, {far:0} away");
                        if (far <= RiderWorld)
                        {
                            this.output.Urge(Announcements.ChaosLiftMoving(moved == ChaosTrial.LiftUp));
                        }
                    }

                    if (this.lifts.Arrived(entity.Id, now) is { } floor && far <= RiderWorld)
                    {
                        this.output.Urge(Announcements.ChaosLiftStopped(floor == ChaosTrial.LiftUp));
                    }
                }

                if (piece == ChaosPiece.Lift && this.lifts.Activated(entity.Id, ChaosTrial.IsActivated(states)) && entity.TryGetComponent<Render>(out var lift))
                {
                    var transition = TransitionState.Of(this.memory, entity);
                    var down = transition?.State == ChaosTrial.LiftUp;
                    this.log.Info($"Lift {entity.Id} activated, transition {transition?.State.ToString() ?? "?"} of {transition?.Count.ToString() ?? "?"}");
                    var place = lift.WorldPosition;
                    var ride = new ScanEntry(entity.Id, ScanCategory.Quest, Announcements.ChaosLiftReady(down), place.X, place.Y, place.Z, OpenedWays.Kind);
                    this.LiftReady?.Invoke(ride);
                    this.output.Say(SpeechKind.Places, this.catalog.Describe(ride with { Kind = string.Empty }));
                }

                if (piece == ChaosPiece.BloodCircle && this.news.Filled(entity.Id, ChaosTrial.IsFull(states)))
                {
                    this.output.Say(SpeechKind.Quest, Announcements.ChaosCircleFilled);
                }
            }

            if (this.exitLever.Poll(now, this.levers) is { } lever && this.leverEntries.TryGetValue(lever, out var way))
            {
                this.log.Info($"Exit lever named: {lever} of {this.levers.Count} levers ({string.Join(", ", this.levers.Select(each => $"{each.Id} {(each.Enabled ? "enabled" : "shut")} {each.Distance:0}"))})");
                this.output.Say(SpeechKind.Places, this.catalog.Describe(way));
            }

            if (this.news.Began(this.pieces) is { } round)
            {
                this.output.Say(SpeechKind.Quest, Announcements.ChaosRoundBegan(round));
            }

            if (!Seats.Followed.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            if (this.news.Core(buffs.StatusEffects.ContainsKey(ChaosTrial.HoldingCoreBuff)) == true)
            {
                this.output.Say(SpeechKind.Quest, Announcements.ChaosCoreTaken);
            }

            var ruin = buffs.StatusEffects.TryGetValue(ChaosTrial.RuinBuff, out var effect) ? PlayerCharges.Held(effect.Charges) : 0;
            if (this.news.Ruin(ruin) is { } count)
            {
                var line = Announcements.ChaosRuin(count, ChaosTrial.RuinToFail);
                this.log.Info($"Ruin: {count} (buff charges {effect.Charges})");
                if (count >= ChaosTrial.RuinToFail - 1)
                {
                    this.output.Urge(line);
                }
                else
                {
                    this.output.Say(SpeechKind.StatusBegins, line);
                }
            }
        }

        private void LogOnce(string path, List<(string Name, long Value)> states)
        {
            var line = $"{path}: {string.Join(", ", states.Select(state => $"{state.Name}={state.Value}"))}";
            if (this.logged.Add(line))
            {
                this.log.Info($"Chaos trial piece seen: {line}");
            }
        }
    }
}
