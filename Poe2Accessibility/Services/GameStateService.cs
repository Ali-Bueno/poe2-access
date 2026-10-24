namespace Poe2Accessibility.Services
{
    using System;
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

        private readonly PluginLog log;
        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private IntPtr lastAreaRow;

        public GameStateService(PluginLog log, HostMemoryReader reader, UiElementReader elements)
        {
            this.reader = reader;
            this.elements = elements;
            this.log = log;
        }

        public event Action<GameStateTypes, GameStateTypes>? StateChanged;

        public event Action<string>? AreaChanged;

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
            var loading = IntPtr.Zero;
            foreach (var (address, type) in Core.States.AllStates)
            {
                if (type == GameStateTypes.AreaLoadingState)
                {
                    loading = address;
                }
            }

            if (loading == IntPtr.Zero ||
                !this.reader.TryRead<IntPtr>(loading + GameLayout.AreaLoadingState.CurrentAreaRowOffset, out var row) ||
                row == this.lastAreaRow || !this.reader.IsValidAddress(row) ||
                !this.reader.TryRead<GameOffsets.Objects.FilesStructures.WorldAreaDatOffsets>(row, out var area))
            {
                return;
            }

            var name = this.elements.ReadUtf16BufferAt(area.NamePtr, MaxAreaNameChars);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            this.lastAreaRow = row;
            this.CurrentAreaName = name;
            this.CurrentAreaId = this.elements.ReadUtf16BufferAt(area.IdPtr, MaxAreaNameChars) ?? string.Empty;
            this.log.Info($"Area: {name} ({this.CurrentAreaId})");
            this.AreaChanged?.Invoke(name);
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
                this.log.Info(pid == 0 ? "Game process lost" : $"Attached to game process {pid}");
                foreach (var (address, type) in Core.States.AllStates)
                {
                    this.log.Debug($"State object {type} at 0x{address.ToInt64():X}");
                }

                this.AttachedChanged?.Invoke(pid != 0);
            }

            var state = Core.States.GameCurrentState;
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
            else if (state is GameStateTypes.SelectCharacterState or GameStateTypes.CreateCharacterState or GameStateTypes.LoginState)
            {
                // Entering the game again (maybe with another character in the same area) names the area again.
                this.lastAreaRow = IntPtr.Zero;
            }
        }
    }
}
