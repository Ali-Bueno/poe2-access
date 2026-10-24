namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameHelper;
    using GameHelper.RemoteEnums;

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

        private const string StatesPart = "game states";

        private readonly PluginLog log;
        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private IntPtr lastAreaRow;
        private int areaRowOffset = GameLayout.AreaLoadingState.CurrentAreaRowOffset;
        private bool areaRowChecked;
        private int areaMisses;
        private long areaSearchedAt = long.MinValue / 2;

        /// <summary>No loading screen or login seen yet: the host (re)started with the game already in an area.</summary>
        private bool resuming = true;

        private long attachedAt;
        private bool stateRead;

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

        /// <summary>GameHelper opened (true) or lost (false) the game process.</summary>
        public event Action<bool>? AttachedChanged;

        public GameStateTypes CurrentState { get; private set; } = GameStateTypes.GameNotLoaded;

        public string CurrentAreaName { get; private set; } = string.Empty;

        /// <summary>World-area id ("G1_1" is The Riverbank); keys the landmark tables.</summary>
        public string CurrentAreaId { get; private set; } = string.Empty;

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
            this.log.Info($"Area: {name} ({this.CurrentAreaId}){(this.resuming ? " resumed" : string.Empty)}");
            this.AreaChanged?.Invoke(name, this.resuming);
            this.resuming = false;
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

        public void Tick()
        {
            var pid = Core.Process.Pid;
            if (pid != this.AttachedPid)
            {
                this.AttachedPid = pid;
                this.attachedAt = Environment.TickCount64;
                this.stateRead = false;
                this.log.Info(pid == 0 ? "Game process lost" : $"Attached to game process {pid}");
                foreach (var (address, type) in Core.States.AllStates)
                {
                    this.log.Debug($"State object {type} at 0x{address.ToInt64():X}");
                }

                this.AttachedChanged?.Invoke(pid != 0);
            }

            var state = Core.States.GameCurrentState;
            if (state != GameStateTypes.GameNotLoaded)
            {
                this.stateRead = true;
            }
            else if (this.IsAttached && !this.stateRead && Environment.TickCount64 - this.attachedAt > StatesUnreadMs)
            {
                this.stateRead = true; // said once per game process
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
    }
}
