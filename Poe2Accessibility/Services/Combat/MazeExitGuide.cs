namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>A word of the way out of a maze: the first one, the way on, or that the player is out.</summary>
    public enum MazeExitNewsKind
    {
        First,
        Way,
        Out,
    }

    public readonly record struct MazeExitNews(MazeExitNewsKind Kind, int Octant, int Steps);

    /// <summary>
    ///     Says the way out of a boss's maze as it turns: at the start, at each wall passed, whenever the next point lies
    ///     another way, and once the player is out of the blast's reach. Pure.
    /// </summary>
    public sealed class MazeExitGuide
    {
        /// <summary>A turn of the same leg is not said again sooner than this (ms).</summary>
        private const int WordGapMs = 700;

        private bool started;
        private bool outSaid;
        private int wall;
        private int octant;
        private long saidAt;

        public void Reset() => this.started = this.outSaid = false;

        /// <param name="isOut">The player is out of the blast's reach.</param>
        public MazeExitNews? Update(MazeStep step, bool isOut, Vector2 player, Func<Vector2, int> octantOf, long now)
        {
            if (isOut)
            {
                var say = this.started && !this.outSaid;
                this.outSaid = true;
                return say ? new MazeExitNews(MazeExitNewsKind.Out, 0, 0) : null;
            }

            this.outSaid = false;
            var way = octantOf(step.At - player);
            var kind = !this.started ? MazeExitNewsKind.First : MazeExitNewsKind.Way;
            if (this.started && step.Wall == this.wall && (way == this.octant || now - this.saidAt < WordGapMs))
            {
                return null;
            }

            (this.started, this.wall, this.octant, this.saidAt) = (true, step.Wall, way, now);
            return new MazeExitNews(kind, way, Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(step.At, player) / BlastGuide.StepWorldUnits)));
        }
    }
}
