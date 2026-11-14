namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader;

    /// <summary>The Delirium fog around the player at one look.</summary>
    /// <param name="Front">How deep the fog's front has gone, in cells of path from the mirror.</param>
    /// <param name="Rear">How deep its rear has come: nearer the mirror than this the fog has faded.</param>
    /// <param name="Most">The deepest place of the area.</param>
    /// <param name="Depth">The player's own depth; null where the map does not say.</param>
    /// <param name="Deeper">The way, in grid cells, to where the ground is deeper: where the fog is going.</param>
    /// <param name="Shallower">The way back toward the mirror.</param>
    /// <param name="Paused">The fog's rear is held still (kills pause it).</param>
    public readonly record struct FogSample(int Front, int Rear, int Most, int? Depth, Vector2? Deeper, Vector2? Shallower, bool Paused);

    /// <summary>
    ///     Reads the Delirium fog from the client's own ruleset (<see cref="GameLayout.Fog"/>): its clocks and speeds, and the
    ///     depth map the server sends, in which the player's depth is looked up exactly as the game's progress bar does. Every
    ///     pointer and number is checked for what it must be; anything that does not fit gives no sample, and the fog is then
    ///     spoken of only by the game's own stat, as before.
    /// </summary>
    public sealed class FogReader
    {
        /// <summary>How far around the player the ground's depth is asked, to tell which way it deepens.</summary>
        private const int ProbeCells = 34;

        /// <summary>A way is told only when the depth differs by this much over the probe.</summary>
        private const int LeastSlope = 6;

        private static readonly (int X, int Y)[] Ways = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

        private readonly HostMemoryReader memory;
        private readonly RulesetReader rulesets;

        public FogReader(HostMemoryReader memory, RulesetReader rulesets)
        {
            this.memory = memory;
            this.rulesets = rulesets;
        }

        /// <summary>Gets why the last look gave no sample, for the log.</summary>
        public string Why { get; private set; } = "not looked";

        /// <summary>Gets whether an encounter with a front and a rear runs: false when its rules are here and say it does not, null when they are not here or do not read.</summary>
        public bool? Running { get; private set; }

        /// <summary>The fog at the player's grid place; null without a running encounter of the kind that has a front and a rear.</summary>
        public FogSample? Read(int x, int y)
        {
            this.Running = null;
            if (this.rulesets.Ruleset(GameLayout.Fog.RulesName) is not { } rules)
            {
                this.Why = "no ruleset";
                return null;
            }

            var block = this.memory.ReadBytes(rules, GameLayout.Fog.RulesBytes);
            if (block.Length < GameLayout.Fog.RulesBytes)
            {
                this.Why = "ruleset unread";
                return null;
            }

            var (active, finished) = (block[GameLayout.Fog.Active], block[GameLayout.Fog.Finished]);
            if (active != 1 || finished != 0)
            {
                this.Why = active > 1 || finished > 1 ? "flags are no flags" : "not running";
                this.Running = active > 1 || finished > 1 ? null : false;
                return null;
            }

            var most = BitConverter.ToInt32(block, GameLayout.Fog.Most);
            var clocks = new FogClocks(
                BitConverter.ToSingle(block, GameLayout.Fog.Time), BitConverter.ToSingle(block, GameLayout.Fog.RearTime), BitConverter.ToSingle(block, GameLayout.Fog.End),
                BitConverter.ToSingle(block, GameLayout.Fog.GainFactor), BitConverter.ToSingle(block, GameLayout.Fog.ExtraDelay), block[GameLayout.Fog.Never] != 0);
            var rowAt = new IntPtr(BitConverter.ToInt64(block, GameLayout.Fog.Balance));
            var area = Core.States.InGameStateObject.CurrentAreaInstance.Address;
            if (most <= 0 || most > GameLayout.Fog.DeepestBelievable || most == FogMath.Unreached ||
                !float.IsFinite(clocks.Time) || clocks.Time < -5f || !float.IsFinite(clocks.RearTime) || float.IsNaN(clocks.End) ||
                !float.IsFinite(clocks.GainFactor) || clocks.GainFactor < 0f || !float.IsFinite(clocks.ExtraDelay) || clocks.ExtraDelay < 0f ||
                !this.memory.IsValidAddress(rowAt) || new IntPtr(BitConverter.ToInt64(block, GameLayout.Fog.Map + GameLayout.Fog.MapArea)) != area)
            {
                this.Why = "clocks, depth or map do not fit";
                return null;
            }

            var row = this.memory.ReadBytes(rowAt, GameLayout.Fog.BalanceBytes);
            if (row.Length < GameLayout.Fog.BalanceBytes)
            {
                this.Why = "balance row unread";
                return null;
            }

            var balance = new FogBalance(
                BitConverter.ToSingle(row, GameLayout.Fog.BalanceFrontSpeed), BitConverter.ToSingle(row, GameLayout.Fog.BalanceRearSpeed),
                BitConverter.ToSingle(row, GameLayout.Fog.BalanceRearDelay), BitConverter.ToSingle(row, GameLayout.Fog.BalanceRearGain),
                BitConverter.ToSingle(row, GameLayout.Fog.BalanceRamp), BitConverter.ToSingle(row, GameLayout.Fog.BalanceFirstSpeed));
            if (!FogMath.IsPlausible(balance))
            {
                this.Why = "balance row is no row";
                return null;
            }

            var front = FogMath.Shown(FogMath.Front(balance, clocks.Time), most);
            var rear = FogMath.Shown(FogMath.Rear(balance, clocks), most);
            var paused = block[GameLayout.Fog.Hold] != 0 || BitConverter.ToSingle(block, GameLayout.Fog.PauseLeft) > 0f;
            if (rear < 0 || rear > front)
            {
                this.Why = "rear past front";
                return null;
            }

            var map = this.MapOf(block);
            var depth = map is { } known ? this.DepthAt(known, x, y, most) : null;
            Vector2? deeper = null;
            Vector2? shallower = null;
            if (map is { } slopes && depth is { } here)
            {
                var (most2, least) = (here + LeastSlope, here - LeastSlope);
                foreach (var (wayX, wayY) in Ways)
                {
                    // A place beyond a thin wall is a long way round: its depth says nothing of the way to walk.
                    if (this.DepthAt(slopes, x + (wayX * ProbeCells), y + (wayY * ProbeCells), most) is not { } there ||
                        Math.Abs(there - here) > (ProbeCells * 3) / 2)
                    {
                        continue;
                    }

                    if (there >= most2)
                    {
                        (most2, deeper) = (there + 1, new Vector2(wayX, wayY));
                    }

                    if (there <= least)
                    {
                        (least, shallower) = (there - 1, new Vector2(wayX, wayY));
                    }
                }
            }

            this.Why = depth == null ? "the player's depth is not on the map" : "read";
            this.Running = true;
            return new FogSample(front, rear, most, depth, deeper, shallower, paused);
        }

        /// <summary>The depth map's parts, checked; null when they are not a map.</summary>
        private DepthMap? MapOf(byte[] rules)
        {
            IntPtr At(int offset) => new(BitConverter.ToInt64(rules, GameLayout.Fog.Map + offset));
            var (header, regions, regionsEnd, cells, cellsEnd, grid) =
                (At(GameLayout.Fog.MapHeader), At(GameLayout.Fog.MapRegions), At(GameLayout.Fog.MapRegions + IntPtr.Size), At(GameLayout.Fog.MapCells),
                 At(GameLayout.Fog.MapCells + IntPtr.Size), At(GameLayout.Fog.MapGrid));
            if (!this.memory.IsValidAddress(header) || !this.memory.IsValidAddress(regions) || !this.memory.IsValidAddress(cells) || !this.memory.IsValidAddress(grid) ||
                !this.memory.TryRead<ushort>(header, out var width) || !this.memory.TryRead<ushort>(header + 2, out var height) ||
                !this.memory.TryRead<ushort>(header + 4, out var originX) || !this.memory.TryRead<ushort>(header + 6, out var originY) ||
                !this.memory.TryRead<IntPtr>(grid + GameLayout.Fog.GridData, out var data) || !this.memory.TryRead<IntPtr>(grid + GameLayout.Fog.GridData + IntPtr.Size, out var dataEnd) ||
                !this.memory.TryRead<uint>(grid + GameLayout.Fog.GridStride, out var stride) || !this.memory.IsValidAddress(data))
            {
                return null;
            }

            var regionBytes = regionsEnd.ToInt64() - regions.ToInt64();
            var cellCount = cellsEnd.ToInt64() - cells.ToInt64();
            var nodeCount = (dataEnd.ToInt64() - data.ToInt64()) / sizeof(float);
            return width is 0 or > GameLayout.Fog.WidestMap || height is 0 or > GameLayout.Fog.WidestMap || cellCount != (long)width * height ||
                   regionBytes <= 0 || regionBytes % GameLayout.Fog.RegionBytes != 0 || regionBytes / GameLayout.Fog.RegionBytes > sbyte.MaxValue ||
                   stride < 2 || stride > (2 * GameLayout.Fog.WidestMap) + 1 || nodeCount < stride * 2 || nodeCount % stride != 0 ||
                   nodeCount / stride > (2 * GameLayout.Fog.WidestMap) + 1
                ? null
                : new DepthMap(width, height, originX, originY, cells, regions, (int)(regionBytes / GameLayout.Fog.RegionBytes), data, nodeCount, (int)stride);
        }

        /// <summary>A place's depth, as the game's bar computes the player's; null off the map, in a region never built, or one the fog does not reach.</summary>
        private int? DepthAt(DepthMap map, int x, int y, int most)
        {
            var (cellX, cellY) = FogMath.CellOf(x, y, map.OriginX, map.OriginY);
            if (x < 0 || y < 0 || cellX < 0 || cellY < 0 || cellX >= map.Width || cellY >= map.Height ||
                !this.memory.TryRead<sbyte>(new IntPtr(map.Cells.ToInt64() + cellX + ((long)map.Width * cellY)), out var region) || region < 0 || region >= map.Regions ||
                !this.memory.TryRead<float>(map.RegionRows + (GameLayout.Fog.RegionBytes * region) + GameLayout.Fog.RegionDepth, out var regionDepth))
            {
                return null;
            }

            if (float.IsPositiveInfinity(regionDepth))
            {
                return null; // the game says 8000 here: a place the fog cannot reach
            }

            var (nodeX, nodeY) = (FogMath.NodeOf(x), FogMath.NodeOf(y));
            var first = ((long)nodeY * map.Stride) + nodeX;
            var second = first + map.Stride;
            if (nodeX < 0 || nodeY < 0 || nodeX + 1 >= map.Stride || second + 1 >= map.Nodes ||
                !this.memory.TryRead<float>(new IntPtr(map.Data.ToInt64() + (first * sizeof(float))), out var v00) ||
                !this.memory.TryRead<float>(new IntPtr(map.Data.ToInt64() + ((first + 1) * sizeof(float))), out var v10) ||
                !this.memory.TryRead<float>(new IntPtr(map.Data.ToInt64() + (second * sizeof(float))), out var v01) ||
                !this.memory.TryRead<float>(new IntPtr(map.Data.ToInt64() + ((second + 1) * sizeof(float))), out var v11))
            {
                return null;
            }

            return FogMath.DepthOf(regionDepth, FogMath.Between(v00, v10, v01, v11, x, y), most);
        }

        private readonly record struct DepthMap(
            int Width, int Height, int OriginX, int OriginY, IntPtr Cells, IntPtr RegionRows, int Regions, IntPtr Data, long Nodes, int Stride);
    }
}
