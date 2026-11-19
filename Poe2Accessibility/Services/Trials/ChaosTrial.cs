namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A piece of a Trial of Chaos round: what the round is fought with, or the way out of its room.</summary>
    public enum ChaosPiece
    {
        SoulCore,
        StoneAltar,
        Statue,
        BloodCircle,
        BloodAltar,
        LiftLever,
        BossLift,

        /// <summary>
        ///     The switch beside the Trialmaster that opens the choice of a modifier and so starts the round
        ///     (<c>UltimatumSelector</c>, named "Interruptor" in the game; seen live 2026-11-19, it sounded nothing).
        /// </summary>
        Selector,

        /// <summary>
        ///     A lift between the Trialmaster's level and a room (<c>LiftPlatform[Large]</c>): `activated` 1 while it can be ridden
        ///     (its minimap icon shows), its transition 2 up and 1 down (LiftPlatformLarge.ot: on_transition ascends). No target:
        ///     ridden by standing on it and pulling its lever.
        /// </summary>
        Lift,
    }

    /// <summary>What a round of the Trial of Chaos asks (UltimatumEncounterTypes), as far as its pieces tell.</summary>
    public enum ChaosRound
    {
        Collect,
        Escort,
        Sacrifice,
    }

    /// <summary>
    ///     The Trial of Chaos' pieces by their files, the states that say one is still to be used, and the buffs that carry a
    ///     round's standing (docs/research/trial-of-chaos.md: the .ot files and BuffDefinitions of 0.5.5f). Pure.
    /// </summary>
    public static class ChaosTrial
    {
        /// <summary>The trial's own objects, by their scripts' folder.</summary>
        public const string Folder = "Metadata/Terrain/Gallows/Act3/3_10/Objects/";

        /// <summary>The buff of a Vaal Soul Core in hand: physical damage that grows until the core is in a Stone Altar.</summary>
        public const string HoldingCoreBuff = "ultimatum_holding_soul_core";

        /// <summary>The buff whose charges are the Ruin the Stalking Shade deals.</summary>
        public const string RuinBuff = "ultimatum_ruin";

        /// <summary>The Ruin that fails the trial: "On reaching 7 Ruin, you will fail the Trials" (BuffDefinitions, ultimatum_ruin).</summary>
        public const int RuinToFail = 7;

        /// <summary>A blood circle's state once full: it fades out and feeds the altar.</summary>
        public const string CircleFilledState = "bloodfilled";

        /// <summary>The blood altar's state: 3 once its three circles have fed it.</summary>
        public const string AltarCompleteState = "complete";

        /// <summary>
        ///     The lift's own sound (a lift arriving, the owner's pick 2026-11-19): the lever beside it rings as a quest object, and the
        ///     two sounded alike.
        /// </summary>
        public const string LiftSound = "lift";

        /// <summary>
        ///     Standing on the lift: its zone, 29 cells of 10.87 world units (LiftPlatformLarge.ot's ControlZone), rounded up. Within
        ///     it the lift is silent, as a boss's shelter is once reached.
        /// </summary>
        public const float LiftZoneWorld = 330f;

        /// <summary>The lift's state while it can be ridden.</summary>
        public const string LiftActivatedState = "activated";

        /// <summary>A lift's transition when it stands up, at the Trialmaster's level: riding it goes down into the room.</summary>
        public const int LiftUp = 2;

        /// <summary>The lever's state while it can be pulled.</summary>
        public const string LeverEnabledState = "enabled";

        private const char InstanceMark = '@';

        private static readonly Dictionary<string, ChaosPiece> Files = new(StringComparer.OrdinalIgnoreCase)
        {
            [Folder + "SoulCoreEncounter/SoulCorePickup"] = ChaosPiece.SoulCore,
            [Folder + "SoulCoreEncounter/SoulCorePlinth"] = ChaosPiece.StoneAltar,
            [Folder + "StatueEncounter/EncounterStatue"] = ChaosPiece.Statue,
            [Folder + "BloodAltarEncounter/BloodCircle"] = ChaosPiece.BloodCircle,
            [Folder + "BloodAltarEncounter/BloodAltar"] = ChaosPiece.BloodAltar,
            [Folder + "LiftButton"] = ChaosPiece.LiftLever,
            [Folder + "BossLiftTransition"] = ChaosPiece.BossLift,
            [Folder + "UltimatumSelector"] = ChaosPiece.Selector,
            [Folder + "LiftPlatform"] = ChaosPiece.Lift,
            [Folder + "LiftPlatformLarge"] = ChaosPiece.Lift,
        };

        /// <summary>Each piece's file and the state matched on it, for the data step's check (<see cref="GameNames"/>).</summary>
        public static IEnumerable<(string File, string? State)> FilesMatched => Files.Select(pair => (pair.Key, pair.Value switch
        {
            ChaosPiece.BloodCircle => CircleFilledState,
            ChaosPiece.BloodAltar => AltarCompleteState,
            ChaosPiece.LiftLever => LeverEnabledState,
            ChaosPiece.Lift => LiftActivatedState,
            _ => (string?)null,
        }));

        /// <summary>The piece an object's path stands for; null for anything else.</summary>
        public static ChaosPiece? PieceOf(string path)
        {
            var mark = path.IndexOf(InstanceMark);
            return Files.TryGetValue(mark < 0 ? path : path[..mark], out var piece) ? piece : null;
        }

        /// <summary>A piece the game gives no target, yet the player goes to it: the statue and circles of a round, a lift.</summary>
        public static bool IsFoughtBeside(ChaosPiece piece) => piece is ChaosPiece.Statue or ChaosPiece.BloodCircle or ChaosPiece.Lift;

        /// <summary>Whether such a piece still asks for the player: a circle until it is full, a lift while it can be ridden, the statue always.</summary>
        public static bool StillAsks(ChaosPiece piece, IEnumerable<(string Name, long Value)> states) => piece switch
        {
            ChaosPiece.BloodCircle => !IsFull(states),
            ChaosPiece.Lift => IsActivated(states),
            _ => true,
        };

        /// <summary>A lift that can be ridden now.</summary>
        public static bool IsActivated(IEnumerable<(string Name, long Value)> states) =>
            states.Any(state => state.Value > 0 && string.Equals(state.Name, LiftActivatedState, StringComparison.Ordinal));

        public static bool IsFull(IEnumerable<(string Name, long Value)> states) =>
            states.Any(state => state.Value > 0 && string.Equals(state.Name, CircleFilledState, StringComparison.Ordinal));

        /// <summary>The round a piece belongs to; null for the ways out.</summary>
        public static ChaosRound? RoundOf(ChaosPiece piece) => piece switch
        {
            ChaosPiece.SoulCore or ChaosPiece.StoneAltar => ChaosRound.Collect,
            ChaosPiece.Statue => ChaosRound.Escort,
            ChaosPiece.BloodCircle or ChaosPiece.BloodAltar => ChaosRound.Sacrifice,
            _ => null,
        };

        /// <summary>
        ///     What rings as the thing to do next and how: a core while none is held, an altar while one is (against the core's
        ///     growing damage, so with the clock's range and guide), a lever while it can be pulled, the modifier switch only
        ///     before the round (its `enabled` goes 0 and it turns untargetable once a modifier is chosen: UltimatumSelector.ot;
        ///     the owner, 2026-11-19: "tiene que sonar solo antes de empezar"), the statue and the circles.
        /// </summary>
        public static (bool Rings, bool Timed) Asks(ChaosPiece piece, bool holdingCore, IEnumerable<(string Name, long Value)>? states = null) => piece switch
        {
            ChaosPiece.SoulCore => (!holdingCore, false),
            ChaosPiece.StoneAltar => (holdingCore, holdingCore),
            ChaosPiece.BloodAltar or ChaosPiece.BossLift => (false, false),
            ChaosPiece.Selector or ChaosPiece.LiftLever => (IsEnabled(states), false),
            _ => (true, false),
        };

        /// <summary>
        ///     A lift lever only calls its lift to the lever's floor; the lift itself goes by the pressure plate on it
        ///     (<c>LiftButton_PlatformLarge</c>, a ControlZone; LiftButton.ot sets the lifts' `called`). With the lift already there, A
        ///     did nothing (the owner, 2026-11-19). A lift belongs to a lever within this reach on the ground's plane.
        /// </summary>
        public const float LeverLiftReachWorld = 600f;

        /// <summary>Heights further apart than this are two floors: the lift down read about 550-630 world units from a rider up (log).</summary>
        public const float FloorGapWorld = 200f;

        /// <summary>
        ///     Whether the lever's lift stands on another floor, so pulling it calls the lift: true or false for the nearest lift
        ///     within reach, null when no lift is near (any lever is then taken as useful). Points are (x, y, height).
        /// </summary>
        public static bool? LiftAway(System.Numerics.Vector3 lever, IEnumerable<System.Numerics.Vector3> lifts)
        {
            var near = lifts.Select(lift => (Lift: lift, Ground: System.Numerics.Vector2.Distance(new(lift.X, lift.Y), new(lever.X, lever.Y))))
                .Where(pair => pair.Ground <= LeverLiftReachWorld).OrderBy(pair => pair.Ground).ToList();
            return near.Count == 0 ? null : Math.Abs(near[0].Lift.Z - lever.Z) > FloorGapWorld;
        }

        /// <summary>A switch or lever that can be used now: its `enabled` state above 0; one whose states were not read is taken as usable.</summary>
        public static bool IsEnabled(IEnumerable<(string Name, long Value)>? states) =>
            states?.ToList() is not { Count: > 0 } read || read.Any(state => state.Value > 0 && string.Equals(state.Name, LeverEnabledState, StringComparison.Ordinal));
    }

    /// <summary>
    ///     What is news of a Trial of Chaos round: its kind when its pieces first appear, a soul core taken and implanted, a
    ///     blood circle filled, and Ruin as it grows. Pure.
    /// </summary>
    public sealed class ChaosRoundNews
    {
        private readonly HashSet<uint> seen = new();
        private readonly HashSet<uint> full = new();
        private ChaosRound? round;
        private bool holding;
        private int ruin;

        /// <summary>A new area: nothing is known of its rounds.</summary>
        public void Reset()
        {
            this.seen.Clear();
            this.full.Clear();
            (this.round, this.holding, this.ruin) = (null, false, 0);
        }

        /// <summary>The round the pieces in sight belong to, once: a round's pieces are new entities, so a second round of a kind is said again.</summary>
        public ChaosRound? Began(IEnumerable<(uint Id, ChaosPiece Piece)> pieces)
        {
            ChaosRound? began = null;
            var any = false;
            foreach (var (id, piece) in pieces)
            {
                if (ChaosTrial.RoundOf(piece) is not { } kind)
                {
                    continue;
                }

                any = true;
                if (this.seen.Add(id) && this.round != kind)
                {
                    began = this.round = kind;
                }
            }

            if (!any)
            {
                this.round = null; // between rounds: the next one is said whatever its kind
            }

            return began;
        }

        /// <summary>A blood circle that has just filled; each once.</summary>
        public bool Filled(uint id, bool isFull) => isFull ? this.full.Add(id) : this.full.Remove(id) && false;

        /// <summary>The core in hand: true as one is taken, false as it leaves the hand, null while nothing changed.</summary>
        public bool? Core(bool holdingNow)
        {
            if (holdingNow == this.holding)
            {
                return null;
            }

            return this.holding = holdingNow;
        }

        /// <summary>The Ruin to say: its count once it has grown, null otherwise (it never falls within a run).</summary>
        public int? Ruin(int count)
        {
            var grew = count > this.ruin;
            this.ruin = count;
            return grew ? count : null;
        }
    }

    /// <summary>
    ///     Which lift lever takes the player on once a room is won: a sighted player sees the way out light up, a blind one heard
    ///     only "Sala completada" (the owner, 2026-11-19: "me costó encontrar la salida"). Each room has two levers, the way in and
    ///     the way out, shut until the round is won (trial-of-chaos.md §1); the one that turned pullable as the round was won, else
    ///     the nearest pullable one. Pure.
    /// </summary>
    public sealed class ExitLeverNews
    {
        /// <summary>The wait after the round is won for the way out to open: the lever's state comes with the server's next news.</summary>
        public const int WaitMs = 1500;

        private readonly Dictionary<uint, (bool Enabled, long Since)> levers = new();
        private long? won;

        /// <summary>A new area: nothing is known of its levers.</summary>
        public void Reset()
        {
            this.won = null;
            this.levers.Clear();
        }

        /// <summary>A room was completed now.</summary>
        public void RoundWon(long now) => this.won = now;

        /// <summary>Gets a value indicating whether a round won still waits for its way on to be named.</summary>
        public bool Pending => this.won != null;

        /// <summary>The way on was named otherwise (a lift turning ridable says where it goes): nothing more for this round.</summary>
        public void Cancel() => this.won = null;

        /// <summary>The lever to name now, once per round won; null otherwise. <paramref name="present"/>: the levers in view.</summary>
        public uint? Poll(long now, IReadOnlyList<(uint Id, bool Enabled, float Distance)> present)
        {
            foreach (var (id, enabled, _) in present)
            {
                if (!this.levers.TryGetValue(id, out var was))
                {
                    this.levers[id] = (enabled, long.MinValue); // first seen: when it turned so is not known
                }
                else if (was.Enabled != enabled)
                {
                    this.levers[id] = (enabled, now);
                }
            }

            if (this.won is not { } at || now - at < WaitMs)
            {
                return null;
            }

            // Waited for as long as it takes: the lever was shut through the round and opened later than 8 s after it was won
            // (live 2026-11-19, never said), so the one that opens after the win is the way on.
            var pullable = present.Where(lever => lever.Enabled).ToList();
            if (pullable.Count == 0)
            {
                return null;
            }

            this.won = null;
            var opened = pullable.Where(lever => this.levers[lever.Id].Since >= at - WaitMs).OrderByDescending(lever => this.levers[lever.Id].Since).ToList();
            return (opened.Count > 0 ? opened[0] : pullable.OrderBy(lever => lever.Distance).First()).Id;
        }
    }

    /// <summary>A lift that became ridable, once each time it does (the owner, 2026-11-19: "no me dice si me lleva a la sala"). Pure.</summary>
    public sealed class LiftNews
    {
        private readonly Dictionary<uint, bool> activated = new();
        /// <summary>
        ///     A trip, up or down, measured live 2026-11-19 (the lever shut as it set off and opened 2.0 s later at the top; set off
        ///     down, it set off up again 2.5 s later): the lift's animations at speed 2.5 (LiftPlatformLarge.ot). Then it waits half a
        ///     second before taking a rider on again.
        /// </summary>
        public const int TripMs = 2000;

        /// <summary>The stop between two trips, measured with them.</summary>
        public const int StopMs = 500;

        /// <summary>Off the deck this long before stepping on again is news: walking along its edge says it once.</summary>
        public const int BoardRearmMs = 2000;

        private readonly Dictionary<uint, int> standing = new();
        private readonly Dictionary<uint, (int Floor, long At)> trips = new();
        private readonly Dictionary<uint, (bool On, long OffSince)> decks = new();
        private readonly Dictionary<uint, long> departed = new();

        public void Reset()
        {
            this.activated.Clear();
            this.standing.Clear();
            this.trips.Clear();
            this.decks.Clear();
            this.departed.Clear();
        }

        /// <summary>
        ///     True when the player has just stepped onto the lift's deck, the part that moves (the owner, 2026-11-19: "ascensor,
        ///     quédate quieto para que funcione"); once a boarding, again only after two seconds off it. Standing there when first
        ///     seen counts as stepping on.
        /// </summary>
        public bool Boarded(uint id, bool onDeck, long now)
        {
            // On the way the deck's height and the rider's part (live 2026-11-19: "quédate quieto" again at each stop below): a
            // trip and the half second after it change nothing.
            if (this.departed.TryGetValue(id, out var left) && now - left < TripMs + StopMs)
            {
                return false;
            }

            var was = this.decks.TryGetValue(id, out var deck) ? deck : (On: false, OffSince: long.MinValue / 2);
            if (onDeck)
            {
                this.decks[id] = (true, was.OffSince);
                return !was.On && now - was.OffSince >= BoardRearmMs;
            }

            this.decks[id] = (false, was.On ? now : was.OffSince);
            return false;
        }

        /// <summary>The floor a lift has just reached (2 up, 1 down) once its trip is over; null otherwise. Once a trip.</summary>
        public int? Arrived(uint id, long now)
        {
            if (!this.trips.TryGetValue(id, out var trip) || now - trip.At < TripMs)
            {
                return null;
            }

            this.trips.Remove(id);
            return trip.Floor;
        }

        /// <summary>
        ///     The lift's new transition when it has just set off (2 going up, 1 going down); null otherwise, and when first seen.
        ///     A player who stays on it rides it up and down again and again (the owner, 2026-11-19), so every trip is news.
        /// </summary>
        public int? Moved(uint id, int? transition, long at = 0)
        {
            if (transition is not { } now)
            {
                return null;
            }

            var news = this.standing.TryGetValue(id, out var was) && was != now ? now : (int?)null;
            this.standing[id] = now;
            if (news is { } floor)
            {
                this.trips[id] = (floor, at);
                this.departed[id] = at;
            }

            return news;
        }

        /// <summary>True when this lift has just turned ridable; what stands when first seen is not news.</summary>
        public bool Activated(uint id, bool now)
        {
            var news = this.activated.TryGetValue(id, out var was) && !was && now;
            this.activated[id] = now;
            return news;
        }
    }
}
