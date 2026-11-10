// <copyright file="UiElementBase.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.UiElement
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using GameReader.Cache;
    using GameOffsets.Objects.UiElement;
    using Ui;
    using Utils;

    /// <summary>
    ///     Points to the Ui Element of the game and reads its data.
    /// </summary>
    public class UiElementBase : RemoteObjectBase
    {
        private IntPtr[] childrenAddresses = Array.Empty<IntPtr>();
        // F-136: cache of materialised UiElementBase children — lazily built by the
        // this[int] indexer, invalidated on address change. Slots are null until
        // first accessed. Eliminates the per-indexer-call new+UpdateData(true) hit
        // that was O(N) per traversal in passive-skill-tree-sized trees.
        private UiElementBase?[] childrenCache = Array.Empty<UiElementBase?>();
        protected uint flags; // IsVisible and ShouldModifyPosition information
        private IntPtr parentAddress;
        private readonly UiElementParents parents;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UiElementBase" /> class.
        /// </summary>
        /// <param name="address">address to the Ui Element of the game.</param>
        /// <param name="parents">parents cache to use for this Ui Element.</param>
        internal UiElementBase(IntPtr address, UiElementParents parents)
            : base(address, true, true)
        {
            this.CleanUpData();
            this.parents = parents;
            if (address != IntPtr.Zero )
            {
                this.UpdateData(true);
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the Ui Element is visible or not.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (!UiElementBaseFuncs.IsVisibleChecker(this.flags))
                {
                    return false;
                }
                if (this.parentAddress == IntPtr.Zero)
                {
                    return true;
                }

                // During transitions, parent might not be cached yet; treat as not visible rather than throw
                return this.TryGetParent(out var parent) && parent != null && parent.IsVisible;
            }
        }

        /// <summary>
        ///     Gets the total number of childrens this Ui Element has.
        /// </summary>
        public int TotalChildrens => this.childrenAddresses.Length;

        public bool TryGetParent([NotNullWhen(true)] out UiElementBase? parent)
        {
            if (this.parentAddress == IntPtr.Zero)
            {
                parent = null;
                return false;
            }

            // Parent caches can be briefly empty while the game changes area/state.
            return this.parents.TryGetParent(this.parentAddress, out parent);
        }

        /// <summary>
        ///     Gets the child Ui Element at specified index.
        ///     returns null in case of invalid index.
        /// </summary>
        /// <param name="i">index of the child Ui Element.</param>
        /// <returns>the child Ui Element.</returns>
        public UiElementBase? this[int i]
        {
            get
            {
                if (this.childrenAddresses.Length <= i)
                {
                    return null;
                }

                // F-136: lazy-cache child UiElementBase. First access constructs and
                // caches; subsequent calls return the cached instance. Cache slots
                // are reset whenever UpdateData re-reads childrenAddresses.
                var cached = this.childrenCache[i];
                if (cached == null)
                {
                    cached = new UiElementBase(this.childrenAddresses[i], this.parents);
                    this.childrenCache[i] = cached;
                }

                return cached;
            }
        }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.childrenAddresses = Array.Empty<IntPtr>();
            // F-136: rebuild cache slots to match the new childrenAddresses length.
            // Existing materialised children are dropped; they'll be re-allocated
            // lazily on next this[int] access if still needed.
            this.childrenCache = new UiElementBase?[this.childrenAddresses.Length];
            this.flags = 0x00;
            this.parentAddress = IntPtr.Zero;
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            this.UpdateData(Core.Process.Handle.ReadMemory<UiElementBaseOffset>(this.Address), hasAddressChanged);
        }

        /// <summary>
        ///     Updates the UiElement data.
        /// </summary>
        /// <param name="data">UiElementBaseOffset structure read from the game memory.</param>
        /// <param name="hasAddressChanged">has the address of this object changed or not.</param>
        /// <exception cref="Exception">Throws an exception if it detects invalid UiElement.</exception>
        protected void UpdateData(UiElementBaseOffset data, bool hasAddressChanged)
        {
            if (data.Self != IntPtr.Zero && data.Self != this.Address)
            {
                throw new Exception($"This (address: {this.Address.ToInt64():X})" +
                                    $"is not a Ui Element. Self Address = {data.Self.ToInt64():X}");
            }

            this.parentAddress = data.ParentPtr;
            this.parents.AddIfNotExists(data.ParentPtr);
            this.childrenAddresses = Core.Process.Handle.ReadStdVector<IntPtr>(data.ChildrensPtr);
            // F-136: rebuild cache slots to match the new childrenAddresses length.
            // Existing materialised children are dropped; they'll be re-allocated
            // lazily on next this[int] access if still needed.
            this.childrenCache = new UiElementBase?[this.childrenAddresses.Length];
            this.flags = data.Flags;
        }
    }
}
