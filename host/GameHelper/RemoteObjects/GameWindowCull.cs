// <copyright file="GameWindowCull.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;
    using GameHelper.Utils;

    /// <summary>
    ///     Reads the Game Window Cull (black bar on each side) values from the game.
    ///     Only reads when game window moves/resize.
    /// </summary>
    public class GameWindowCull : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GameWindowCull" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal GameWindowCull(IntPtr address)
            : base(address)
        {
            CoroutineHandler.Start(this.OnGameMove(), priority: int.MaxValue);
            CoroutineHandler.Start(this.OnGameForegroundChange(), priority: int.MaxValue);
        }

        /// <summary>The part that places the UI inside the window.</summary>
        public const string Part = "window cull";

        /// <summary>
        ///     Gets the current game window scale values.
        /// </summary>
        public int Value { get; private set; }

        /// <summary>Gets whether the value read can be a cull of this window: a broken pattern reads another number, or nothing.</summary>
        public StructOutcome Judged { get; private set; } = new(Part, StructHealth.NotJudged, 0, 0, "no window yet");

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.Judge(null);
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            this.Judge(this.Address != IntPtr.Zero && reader.TryReadMemory<int>(this.Address, out var value) ? value : null);
        }

        /// <summary>
        ///     Keeps the pattern's value while it is a cull this window can have (one side's bar narrower than half the window);
        ///     otherwise reads none, said lost. Nothing else in memory gives the cull: 2560 x 1440 reads 0, so it is not the
        ///     16:10 base resolution's bar (128) (2026-11-08).
        /// </summary>
        private void Judge(int? read)
        {
            var width = Core.Process.WindowArea.Width;
            if (width <= 0 || this.Address == IntPtr.Zero)
            {
                (this.Value, this.Judged) = (0, new StructOutcome(Part, StructHealth.NotJudged, 0, 0, "no window or no pattern"));
            }
            else if (read is { } value && value >= 0 && value * 2 < width)
            {
                (this.Value, this.Judged) = (value, new StructOutcome(Part, StructHealth.Intact, value, value, string.Empty));
            }
            else
            {
                (this.Value, this.Judged) = (0, new StructOutcome(Part, StructHealth.Lost, read ?? 0, 0, "no plausible cull"));
            }
        }

        private IEnumerator<Wait> OnGameMove()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnMoved);
                try
                {
                    // No need to check for IntPtr.zero
                    // because game will only move when it exists. :D
                    this.UpdateData(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameWindowCull.OnGameMove] {ex}");
                }
            }
        }

        private IEnumerator<Wait> OnGameForegroundChange()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnForegroundChanged);
                try
                {
                    // No need to check for IntPtr.zero
                    // because game will only move when it exists. :D
                    this.UpdateData(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameWindowCull.OnGameForegroundChange] {ex}");
                }
            }
        }
    }
}