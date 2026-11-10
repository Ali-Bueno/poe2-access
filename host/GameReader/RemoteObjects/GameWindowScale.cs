// <copyright file="GameWindowScale.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Initially this class reads the game window scale value from the game.
    ///     But that was PITA since it's memory location changed a lot. So, this
    ///     class was updated to just calculate the game window scale value in the
    ///     same way the game does it.
    /// </summary>
    public class GameWindowScale : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GameWindowScale" /> class.
        /// </summary>
        internal GameWindowScale()
            : base(IntPtr.Zero)
        {
            CoroutineHandler.Start(this.OnGameMove(), priority: int.MaxValue - 1);
            CoroutineHandler.Start(this.OnGameForegroundChange(), priority: int.MaxValue - 1);
        }

        private float[] valuesSnap = new float[6] { 1f, 1f, 1f, 1f, 1f, 1f };

        /// <summary>
        ///     Gets the Scale Value depending on the index and multiplier.
        /// </summary>
        /// <param name="index">index read from the Ui-Element.</param>
        /// <param name="multiplier">multiplier read from the Ui-Element.</param>
        /// <returns>Returns the Width and Height scale value valid for the specific Ui-Element.</returns>
        public (float WidthScale, float HeightScale) GetScaleValue(int index, float multiplier)
        {
            var snap = System.Threading.Volatile.Read(ref this.valuesSnap);
            var widthScale = multiplier;
            var heightScale = multiplier;
            switch (index)
            {
                case 1:
                    widthScale *= snap[0];
                    heightScale *= snap[1];
                    break;
                case 2:
                    widthScale *= snap[2];
                    heightScale *= snap[3];
                    break;
                case 3:
                    widthScale *= snap[4];
                    heightScale *= snap[5];
                    break;
            }

            return (widthScale, heightScale);
        }

        internal void Refresh()
        {
            this.UpdateData(false);
        }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            System.Threading.Volatile.Write(ref this.valuesSnap, new float[6] { 1f, 1f, 1f, 1f, 1f, 1f });
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            if (Core.Process.WindowArea.Width <= 0 || Core.Process.WindowArea.Height <= 0)
            {
                return;
            }

            // All of the code below is written after RE-ing the game function.
            var v1 = (float)((Core.Process.WindowArea.Width - Core.GameCull.Value - Core.GameCull.Value) / UiElementBaseFuncs.BaseResolution.X);
            var v2 = (float)((Core.Process.WindowArea.Height - 0) / UiElementBaseFuncs.BaseResolution.Y);
            var newValues = new float[6] { v1, v1, v2, v2, v1, v2 };
            System.Threading.Volatile.Write(ref this.valuesSnap, newValues);
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
                    Console.WriteLine($"[GameWindowScale.OnGameMove] {ex}");
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
                    Console.WriteLine($"[GameWindowScale.OnGameForegroundChange] {ex}");
                }
            }
        }
    }
}
