namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.Utils;

    /// <summary>
    ///     Diff-gated view of the host's game state. GameHelper2 keeps its StateChanged event internal, so we
    ///     poll Core.States once per frame and raise our own events only on real changes.
    /// </summary>
    public sealed class GameStateService
    {
        /// <summary>Area names are short ("The Riverbank"); a longer read is not a name.</summary>
        private const int MaxAreaNameChars = 128;

        private const int MaxAreaIdChars = 64;
        private const int AreaSearchRadius = 0x400;
        private const int AreaSearchStep = 8;
        private const int AreaSearchRetryMs = 5000;
        private const int AreaMissesToReport = 3;
        private const string AreaRowPart = "area name";

        /// <summary>
        ///     Attached this long without ever reading a game state: GameHelper's byte patterns or state offsets no longer
        ///     fit the game (a patch); the game reaches its login screen well within it, even compiling shaders.
        /// </summary>
        private const int StatesUnreadMs = 120_000;

        /// <summary>
        ///     While the game has made no state object at all (the host knows where it will be and reads nothing there) the two
        ///     minutes prove nothing: a client that patches itself before its login screen takes longer. Said all the same after
        ///     this long, in case the place is the wrong one and holds nothing for good.
        /// </summary>
        private const int StatesAwaitedMs = 600_000;

        private const string StatesPart = "game states";

        private readonly PluginLog log;
        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private IntPtr lastAreaRow;
        private readonly AreaInstanceGate instances = new();
        private int areaRowOffset = GameLayout.AreaLoadingState.CurrentAreaRowOffset;
        private bool areaRowChecked;
        private int areaMisses;
        private long areaSearchedAt = long.MinValue / 2;

        /// <summary>No loading screen or login seen yet: the host (re)started with the game already in an area.</summary>
        private bool resuming = true;

        private long attachedAt;
        private bool stateRead;

        /// <summary>The states were said lost for this game process: taken back if one is read after all.</summary>
        private bool statesSaidLost;
        private IReadOnlyList<StructOutcome>? layoutNoted;
        private StructOutcome cullNoted;

        public GameStateService(PluginLog log, HostMemoryReader reader, UiElementReader elements)
        {
            this.reader = reader;
            this.elements = elements;
            this.log = log;
        }

        /// <summary>Where layout outcomes go (the area row found again after a game update).</summary>
        public CompatibilityReport? Report { get; set; }

        public event Action<GameStateTypes, GameStateTypes>? StateChanged;

        /// <summary>The area's name, and whether the host just started inside it (no loading screen: nothing is news).</summary>
        public event Action<string, bool>? AreaChanged;

        /// <summary>
        ///     The game loaded another instance of the area it was already in, by its hash (<see cref="AreaInstanceGate"/>):
        ///     no new area to name, but every entity id and address of the old one is void.
        /// </summary>
        public event Action<string>? InstanceChanged;

        /// <summary>GameHelper opened (true) or lost (false) the game process.</summary>
        public event Action<bool>? AttachedChanged;

        public GameStateTypes CurrentState { get; private set; } = GameStateTypes.GameNotLoaded;

        public string CurrentAreaName { get; private set; } = string.Empty;

        /// <summary>World-area id ("G1_1" is The Riverbank); keys the landmark tables.</summary>
        public string CurrentAreaId { get; private set; } = string.Empty;

        /// <summary>Whether the current area is a town or a hideout, from its own row (<see cref="AreaKind"/>).</summary>
        public bool InTownOrHideout { get; private set; }

        public uint AttachedPid { get; private set; }

        public bool IsAttached => this.AttachedPid != 0;

        public static bool IsInGame(GameStateTypes state) =>
            state is GameStateTypes.InGameState or GameStateTypes.EscapeState;

        /// <summary>
        ///     The world-area row the loading state points at (GameLayout.AreaLoadingState); a new row is a new area.
        ///     GameHelper's own CurrentAreaName reads a stale offset in 0.5.5e.
        /// </summary>
        private void TrackArea()
        {
            var loading = LoadingStateAddress();
            if (loading == IntPtr.Zero)
            {
                return;
            }

            var offset = this.AreaRowOffset(loading);
            if (!this.reader.TryRead<IntPtr>(loading + offset, out var held) || held == this.lastAreaRow ||
                this.AreaRowAt(loading, offset) is not var (row, name, id))
            {
                return;
            }

            this.lastAreaRow = row;
            this.CurrentAreaName = name;
            this.CurrentAreaId = id;
            this.InTownOrHideout = AreaKind.IsTownOrHideout(id, this.reader.TryRead<byte>(row + GameLayout.WorldAreaRow.IsTownOffset, out var town) && town != 0);
            this.log.Info($"Area: {name} ({this.CurrentAreaId}){(this.resuming ? " resumed" : string.Empty)}");
            this.instances.AreaEntered(Core.States.InGameStateObject.CurrentAreaInstance.AreaHash);
            this.AreaChanged?.Invoke(name, this.resuming);
            this.resuming = false;
        }

        /// <summary>The instance the host holds: another one under the same area row is told once (a row that changed told it already).</summary>
        private void TrackInstance()
        {
            var hash = Core.States.InGameStateObject.CurrentAreaInstance.AreaHash;
            if (this.instances.Changed(hash))
            {
                this.log.Info($"Instance: {hash} of {this.CurrentAreaName} ({this.CurrentAreaId}), another than the one entered");
                this.InstanceChanged?.Invoke(hash);
            }
        }

        /// <summary>A new game process (perhaps a patched one): the area row is checked again.</summary>
        public void ResetLayout()
        {
            (this.areaRowChecked, this.areaMisses, this.areaSearchedAt) = (false, 0, long.MinValue / 2);
        }

        /// <summary>
        ///     Research (dev command "layout"): every other offset of the loading state within reach that also holds a world-area
        ///     row; any of them could be taken for the area after an update moves the field.
        /// </summary>
        public string SelfTestAreaRow()
        {
            var loading = LoadingStateAddress();
            if (loading == IntPtr.Zero || this.AreaRowAt(loading, this.areaRowOffset) is null)
            {
                return "area name not readable";
            }

            var others = OffsetSearch.Outward(this.areaRowOffset, AreaSearchRadius, AreaSearchStep)
                .Where(at => at != this.areaRowOffset && this.AreaRowAt(loading, at) != null).Order().Select(at => $"+0x{at:X}").ToList();
            return others.Count == 0 ? "area name ok" : $"area name FAIL (also fits at {string.Join(", ", others)})";
        }

        /// <summary>
        ///     Where the loading state keeps the current area row: as measured, else (only in game, when it must hold the
        ///     area just entered) the nearest offset that holds one.
        /// </summary>
        private int AreaRowOffset(IntPtr loading)
        {
            if (this.areaRowChecked || !IsInGame(Core.States.GameCurrentState))
            {
                return this.areaRowOffset;
            }

            if (this.AreaRowAt(loading, this.areaRowOffset) != null)
            {
                this.areaRowChecked = true;
                this.Report?.Found(AreaRowPart);
                return this.areaRowOffset;
            }

            var now = Environment.TickCount64;
            if (now - this.areaSearchedAt < AreaSearchRetryMs)
            {
                return this.areaRowOffset;
            }

            this.areaSearchedAt = now;
            var measured = GameLayout.AreaLoadingState.CurrentAreaRowOffset;
            if (OffsetSearch.First(measured, AreaSearchRadius, AreaSearchStep, at => this.AreaRowAt(loading, at) != null) is { } found)
            {
                (this.areaRowOffset, this.areaRowChecked) = (found, true);
                this.Report?.Moved(AreaRowPart, measured, found);
            }
            else if (++this.areaMisses == AreaMissesToReport)
            {
                this.Report?.Missing(AreaRowPart);
            }

            return this.areaRowOffset;
        }

        /// <summary>The world-area row held at the offset: a readable name and an id shaped like one ("G1_1", "Abyss_Boss1").</summary>
        private (IntPtr Row, string Name, string Id)? AreaRowAt(IntPtr loading, int offset)
        {
            if (!this.reader.TryRead<IntPtr>(loading + offset, out var row) || !this.reader.IsValidAddress(row) ||
                !this.reader.TryRead<GameOffsets.Objects.FilesStructures.WorldAreaDatOffsets>(row, out var area))
            {
                return null;
            }

            var id = this.elements.ReadUtf16BufferAt(area.IdPtr, MaxAreaNameChars);
            var name = this.elements.ReadUtf16BufferAt(area.NamePtr, MaxAreaNameChars);
            return !string.IsNullOrEmpty(name) && id is { Length: >= 2 and <= MaxAreaIdChars } && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                ? (row, name, id)
                : null;
        }

        private static IntPtr LoadingStateAddress()
        {
            var loading = IntPtr.Zero;
            foreach (var (address, type) in Core.States.AllStates)
            {
                if (type == GameStateTypes.AreaLoadingState)
                {
                    loading = address;
                }
            }

            return loading;
        }

        /// <summary>Address of the game's state object for the current state (needed to find its UI root).</summary>
        public static IntPtr CurrentStateObjectAddress()
        {
            // The host reads it off the state stack; looking a name up comes second, since several states can share
            // "unknown" and a state a patch added has no name at all.
            if (Core.States.CurrentStateAddress != IntPtr.Zero)
            {
                return Core.States.CurrentStateAddress;
            }

            var current = Core.States.GameCurrentState;
            foreach (var pair in Core.States.AllStates)
            {
                if (pair.Value == current)
                {
                    return pair.Key;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>Whether no state read for <paramref name="elapsedMs"/> since attach means the states are lost (see the two limits).</summary>
        public static bool StatesOverdue(long elapsedMs, bool awaitsStateObject) =>
            elapsedMs > (awaitsStateObject ? StatesAwaitedMs : StatesUnreadMs);

        public void Tick()
        {
            var pid = Core.Process.Pid;
            if (pid != this.AttachedPid)
            {
                this.AttachedPid = pid;
                this.attachedAt = Environment.TickCount64;
                this.stateRead = false;
                this.statesSaidLost = false;
                this.log.Info(pid == 0 ? "Game process lost" : $"Attached to game process {pid}");
                foreach (var (address, type) in Core.States.AllStates)
                {
                    this.log.Debug($"State object {type} at 0x{address.ToInt64():X}");
                }

                this.AttachedChanged?.Invoke(pid != 0);
            }

            this.NoteLayout();
            var state = Core.States.GameCurrentState;
            if (state != GameStateTypes.GameNotLoaded)
            {
                this.stateRead = true;
                if (this.statesSaidLost)
                {
                    // A client that took longer than the limit to patch itself: the part is not left recorded as lost.
                    this.statesSaidLost = false;
                    this.Report?.Found(StatesPart);
                }
            }
            else if (this.IsAttached && !this.stateRead && StatesOverdue(Environment.TickCount64 - this.attachedAt, Core.States.AwaitsStateObject))
            {
                this.stateRead = true; // said once per game process
                this.statesSaidLost = true;
                this.Report?.Missing(StatesPart);
            }

            if (state != this.CurrentState)
            {
                var previous = this.CurrentState;
                this.CurrentState = state;
                this.log.Info($"State {previous} -> {state}");
                this.StateChanged?.Invoke(previous, state);
            }

            if (IsInGame(state))
            {
                this.TrackArea();
                this.TrackInstance();
            }
            else
            {
                // A loading screen or login before the game: the next area is entered, not resumed.
                this.resuming &= state == GameStateTypes.GameNotLoaded;
                if (state is GameStateTypes.SelectCharacterState or GameStateTypes.CreateCharacterState or GameStateTypes.LoginState)
                {
                    // Entering the game again (maybe with another character in the same area) names the area again.
                    this.lastAreaRow = IntPtr.Zero;
                }
            }
        }

        /// <summary>What the host found of the state object (its stack, its list, the states in play and loading), each once.</summary>
        private void NoteLayout()
        {
            var outcomes = Core.States.LayoutOutcomes;
            var cull = Core.GameCull.Judged;
            if ((ReferenceEquals(outcomes, this.layoutNoted) && cull == this.cullNoted) || this.Report == null)
            {
                return;
            }

            if (cull != this.cullNoted && cull.Health == StructHealth.Lost)
            {
                this.log.Info($"Window cull: the pattern read {cull.From}, {cull.Detail}");
            }

            (this.layoutNoted, this.cullNoted) = (outcomes, cull);
            foreach (var outcome in outcomes.Append(cull))
            {
                this.Report.Record(outcome);
            }
        }
    }
}

