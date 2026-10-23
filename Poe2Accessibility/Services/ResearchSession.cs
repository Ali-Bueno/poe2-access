namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Hands-free research: dumps the UI tree once per screen after it settles and records every focused
    ///     widget's surroundings, so a normal play session documents the menus without hotkeys.
    /// </summary>
    public sealed class ResearchSession
    {
        /// <summary>Screens keep building for a moment after the state flips; dump once they have settled.</summary>
        private const int AutoDumpDelayMs = 2500;

        private readonly UiTreeDumper dumper;
        private readonly FocusResearchLog focusLog;
        private readonly PluginLog log;
        /// <summary>One dump per (state, root-child panel) so opening a sub-screen such as options gets its own dump.</summary>
        private readonly HashSet<(GameStateTypes State, int Panel)> dumped = new();
        private Stopwatch? settleTimer;
        private int pendingPanel = -1;

        public ResearchSession(UiTreeDumper dumper, FocusResearchLog focusLog, PluginLog log)
        {
            this.dumper = dumper;
            this.focusLog = focusLog;
            this.log = log;
        }

        public bool Enabled { get; set; } = true;

        public void OnStateChanged(GameStateTypes current)
        {
            if (current == GameStateTypes.GameNotLoaded)
            {
                this.dumped.Clear();
                this.settleTimer = null;
            }
            else
            {
                this.pendingPanel = -1;
                this.settleTimer = Stopwatch.StartNew();
            }
        }

        public void OnFocusChanged(GameStateTypes state, AccessibleUiNode node, int panelIndex)
        {
            if (!this.Enabled)
            {
                return;
            }

            this.focusLog.Record(state, node);
            if (panelIndex >= 0 && !this.dumped.Contains((state, panelIndex)))
            {
                this.pendingPanel = panelIndex;
                this.settleTimer = Stopwatch.StartNew();
            }
        }

        /// <summary>How often the focused widget's bytes are compared (finds state fields: dropdown highlight, toggles).</summary>
        private const int ByteWatchIntervalMs = 150;

        /// <summary>Largest widget object seen (combo box); allocator strides put neighbours right after it.</summary>
        private const int ByteWatchBytes = 0x800;

        private readonly Stopwatch sinceByteWatch = Stopwatch.StartNew();
        private IntPtr watchedAddress;
        private byte[] watchedBytes = Array.Empty<byte>();

        /// <summary>Call every frame while attached with the focused element (zero when none).</summary>
        public void WatchFocusedBytes(IntPtr focused, UiElementReader elements)
        {
            if (!this.Enabled || focused == IntPtr.Zero || this.sinceByteWatch.ElapsedMilliseconds < ByteWatchIntervalMs)
            {
                return;
            }

            this.sinceByteWatch.Restart();
            var now = elements.ReadBlock(focused, ByteWatchBytes);
            if (focused != this.watchedAddress || now.Length != this.watchedBytes.Length)
            {
                this.watchedAddress = focused;
                this.watchedBytes = now;
                return;
            }

            var changes = new System.Text.StringBuilder();
            for (var i = 0; i < now.Length; i++)
            {
                if (now[i] != this.watchedBytes[i])
                {
                    var start = i;
                    while (i + 1 < now.Length && now[i + 1] != this.watchedBytes[i + 1])
                    {
                        i++;
                    }

                    changes.Append($" +0x{start:X3}[{i - start + 1}]: {Convert.ToHexString(this.watchedBytes, start, i - start + 1)} -> {Convert.ToHexString(now, start, i - start + 1)}");
                }
            }

            if (changes.Length > 0)
            {
                this.focusLog.Note($"bytes changed in focused 0x{focused.ToInt64():X}:{changes}");
                this.watchedBytes = now;
            }
        }

        /// <summary>Call every frame while attached.</summary>
        public void Tick(GameStateTypes state, IntPtr stateObject, string rootOverrideHex, int maxDepth, int maxNodes)
        {
            if (!this.Enabled || this.settleTimer == null || this.settleTimer.ElapsedMilliseconds < AutoDumpDelayMs)
            {
                return;
            }

            this.settleTimer = null;
            var key = (state, this.pendingPanel);
            if (stateObject == IntPtr.Zero || !this.dumped.Add(key) || Core.GHSettings.ShowMemoryDiagnostics)
            {
                return;
            }

            try
            {
                this.dumper.Dump(state, stateObject, rootOverrideHex, false, maxDepth, maxNodes);
            }
            catch (Exception ex)
            {
                this.log.Error("Automatic UI dump failed", ex);
            }
        }
    }
}
