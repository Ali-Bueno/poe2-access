// <copyright file="InGameState.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States
{
    using System;
    using System.Collections.Generic;
    using Coroutine;
    using CoroutineEvents;
    using GameOffsets.Objects.States;
    using InGameStateObjects;
    using GameReader.Utils;

    /// <summary>
    ///     Reads InGameState Game Object.
    /// </summary>
    public class InGameState : RemoteObjectBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="InGameState" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal InGameState(IntPtr address)
            : base(address)
        {
            CoroutineHandler.Start(
                this.OnPerFrame(), "[InGameState] Update Game State", int.MaxValue - 2);
        }

        /// <summary>
        ///     Gets the data related to the currently loaded world area.
        /// </summary>
        public WorldData CurrentWorldInstance { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the data related to the current area instance.
        /// </summary>
        public AreaInstance CurrentAreaInstance { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the UiRoot main child which contains all the UiElements of the game.
        /// </summary>
        public ImportantUiElements GameUi { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the address of the entity currently under the mouse cursor (monsters included), zero when nothing is
        ///     hovered. Only the address is read each frame: <see cref="ReadMouseOverEntity"/> reads the entity itself.
        /// </summary>
        public IntPtr MouseOverEntityAddress { get; private set; }

        /// <summary>Reads the entity under the mouse cursor now, with its components (an empty entity when nothing is hovered).</summary>
        internal Entity ReadMouseOverEntity() => new(this.MouseOverEntityAddress);

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.CurrentAreaInstance.Address = IntPtr.Zero;
            this.GameUi.Address = IntPtr.Zero;
            this.CurrentWorldInstance.Address = IntPtr.Zero;
            this.MouseOverEntityAddress = IntPtr.Zero;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = reader.ReadMemory<InGameStateOffset>(this.Address);
            this.CurrentAreaInstance.Address = data.AreaInstanceData;
            this.CurrentWorldInstance.Address = data.WorldData;

            Core.GHSettings.EnableControllerMode = data.UiRootStructPtr == IntPtr.Zero;
            var uiManagerPtr = Core.GHSettings.EnableControllerMode
                ? data.GamepadUiRootStructPtr
                : data.UiRootStructPtr;

            this.GameUi.Address = uiManagerPtr;

            this.MouseOverEntityAddress = this.ReadMouseOverEntityAddress(reader, data.MouseOverHostPtr);
        }

        /// <summary>
        ///     Follows the MouseOver pointer chain (host -> sub -> entity) and returns the
        ///     address of the entity currently under the cursor, or <see cref="IntPtr.Zero" />
        ///     when nothing is hovered or any hop is invalid.
        /// </summary>
        private IntPtr ReadMouseOverEntityAddress(SafeMemoryHandle reader, IntPtr hostPtr)
        {
            if (hostPtr == IntPtr.Zero ||
                !reader.TryReadMemory<MouseOverHostStruct>(hostPtr, out var host) ||
                host.MouseOverSubPtr == IntPtr.Zero ||
                !reader.TryReadMemory<MouseOverSubStruct>(host.MouseOverSubPtr, out var sub))
            {
                return IntPtr.Zero;
            }

            return sub.EntityPtr;
        }

        private IEnumerator<Wait> OnPerFrame()
        {
            // TODO optimization: convert this into OnAreaChange.
            while (true)
            {
                yield return new Wait(GameHelperEvents.PerFrameDataUpdate);
                try
                {
                    if (this.Address != IntPtr.Zero)
                    {
                        this.UpdateData(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[InGameState.OnPerFrame] {ex}");
                }
            }
        }
    }
}
