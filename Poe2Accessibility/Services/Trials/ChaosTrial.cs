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
        };

        /// <summary>Each piece's file and the state matched on it, for the data step's check (<see cref="GameNames"/>).</summary>
        public static IEnumerable<(string File, string? State)> FilesMatched => Files.Select(pair => (pair.Key, pair.Value switch
        {
            ChaosPiece.BloodCircle => CircleFilledState,
            ChaosPiece.BloodAltar => AltarCompleteState,
            ChaosPiece.LiftLever => LeverEnabledState,
            _ => (string?)null,
        }));

        /// <summary>The piece an object's path stands for; null for anything else.</summary>
        public static ChaosPiece? PieceOf(string path)
        {
            var mark = path.IndexOf(InstanceMark);
            return Files.TryGetValue(mark < 0 ? path : path[..mark], out var piece) ? piece : null;
        }

        /// <summary>A piece the game gives no target and no minimap mark, yet the round is fought beside it.</summary>
        public static bool IsFoughtBeside(ChaosPiece piece) => piece is ChaosPiece.Statue or ChaosPiece.BloodCircle;

        /// <summary>Whether a piece fought beside still asks for the player: a circle until it is full, the statue always.</summary>
        public static bool StillAsks(ChaosPiece piece, IEnumerable<(string Name, long Value)> states) =>
            piece != ChaosPiece.BloodCircle || !IsFull(states);

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
        ///     growing damage, so with the clock's range and guide), a lever while it can be pulled, the statue and the circles.
        /// </summary>
        public static (bool Rings, bool Timed) Asks(ChaosPiece piece, bool holdingCore) => piece switch
        {
            ChaosPiece.SoulCore => (!holdingCore, false),
            ChaosPiece.StoneAltar => (holdingCore, holdingCore),
            ChaosPiece.BloodAltar or ChaosPiece.BossLift => (false, false),
            _ => (true, false),
        };
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
}
