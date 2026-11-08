// <copyright file="PluginUiElementReflection.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Reflection;
    using GameReader.RemoteEnums;
    using GameReader.Utils;
    using GameOffsets.Objects.States.InGameState;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     Resolves internal GameHelper UI types for plugins loaded from a separate assembly.
    /// </summary>
    public static class PluginUiElementReflection
    {
        /// <summary>
        ///     Resolves a UI element rectangle directly from its raw parent chain.
        ///     Plugin-created parent caches are intentionally empty, so relying on
        ///     <c>UiElementBase.Position</c> can return only the element's local position.
        /// </summary>
        public static bool TryGetAbsoluteRect(IntPtr address, out Vector2 position, out Vector2 size) =>
            TryGetAbsoluteRect(address, null, out position, out size);

        /// <summary>
        ///     The positions of the parent chains walked so far, for a caller placing many elements of one screen at once:
        ///     each ancestor is read once instead of once per element under it.
        /// </summary>
        public sealed class RectCache
        {
            internal readonly Dictionary<IntPtr, (Vector2 Position, UiElementBaseOffset Data)> Positions = new();
        }

        /// <summary>Same, reading each ancestor once per <paramref name="cache"/>.</summary>
        public static bool TryGetAbsoluteRect(IntPtr address, RectCache? cache, out Vector2 position, out Vector2 size)
        {
            position = Vector2.Zero;
            size = Vector2.Zero;

            if (!TryGetUnscaledPosition(address, 0, cache, out var unscaledPosition, out var data))
            {
                return false;
            }

            var (widthScale, heightScale) = Core.GameScale.GetScaleValue(data.ScaleIndex, data.LocalScaleMultiplier);
            position = new Vector2(
                (unscaledPosition.X * widthScale) + Core.GameCull.Value,
                unscaledPosition.Y * heightScale);
            size = new Vector2(data.UnscaledSize.X * widthScale, data.UnscaledSize.Y * heightScale);
            return float.IsFinite(position.X) && float.IsFinite(position.Y) &&
                   float.IsFinite(size.X) && float.IsFinite(size.Y) &&
                   size.X > 0f && size.Y > 0f;
        }

        private static bool TryGetUnscaledPosition(
            IntPtr address,
            int depth,
            RectCache? cache,
            out Vector2 position,
            out UiElementBaseOffset data)
        {
            position = Vector2.Zero;
            data = default;
            if (address == IntPtr.Zero || depth >= 64)
            {
                return false;
            }

            if (cache != null && cache.Positions.TryGetValue(address, out var known))
            {
                (position, data) = known;
                return true;
            }

            if (!Core.Process.Handle.TryReadMemory<UiElementBaseOffset>(address, out data))
            {
                return false;
            }
            if ((data.Self != IntPtr.Zero && data.Self != address) ||
                !UiElementBaseFuncs.IsVisibleChecker(data.Flags))
            {
                return false;
            }

            var relative = new Vector2(data.RelativePosition.X, data.RelativePosition.Y);
            if (data.ParentPtr == IntPtr.Zero)
            {
                position = relative;
                cache?.Positions.Add(address, (position, data));
                return true;
            }

            if (!TryGetUnscaledPosition(data.ParentPtr, depth + 1, cache, out var parentPosition, out var parentData))
            {
                return false;
            }

            if (UiElementBaseFuncs.ShouldModifyPos(data.Flags))
            {
                parentPosition += new Vector2(parentData.PositionModifier.X, parentData.PositionModifier.Y);
            }

            if (parentData.ScaleIndex == data.ScaleIndex &&
                parentData.LocalScaleMultiplier == data.LocalScaleMultiplier)
            {
                position = parentPosition + relative;
                cache?.Positions.Add(address, (position, data));
                return true;
            }

            var (parentScaleW, parentScaleH) = Core.GameScale.GetScaleValue(
                parentData.ScaleIndex,
                parentData.LocalScaleMultiplier);
            var (scaleW, scaleH) = Core.GameScale.GetScaleValue(data.ScaleIndex, data.LocalScaleMultiplier);
            if (scaleW == 0f || scaleH == 0f)
            {
                return false;
            }

            position = new Vector2(
                (parentPosition.X * parentScaleW / scaleW) + relative.X,
                (parentPosition.Y * parentScaleH / scaleH) + relative.Y);
            cache?.Positions.Add(address, (position, data));
            return true;
        }
    }
}
