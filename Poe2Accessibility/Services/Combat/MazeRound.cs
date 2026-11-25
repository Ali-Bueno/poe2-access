namespace Poe2Accessibility.Services
{
    using System.Numerics;

    /// <summary>What a maze's round asks of the player now.</summary>
    public enum MazePhase
    {
        None,

        /// <summary>The first blast comes: stand in its hole.</summary>
        Hole,

        /// <summary>Rings stand: out of them, and out of the second blast's reach.</summary>
        Rings,

        /// <summary>Lines stand: through them to beyond the second blast.</summary>
        Lines,
    }

    /// <summary>
    ///     One round of Benedictus's maze, told by the effects the game draws (docs/research/boss-mechanics.md, "Benedictus"):
    ///     a blast that spares a hole, 5 s; rock walls as it falls; a second blast where they stand, 5 s. Pure.
    /// </summary>
    public sealed class MazeRound
    {
        /// <summary>The effect files that tell the two blasts (the skills' own, catalog/telegraph-skills.txt).</summary>
        public const string HoleEffect = "maze_donut_blast.ao";
        public const string RingEffect = "maze_detonation_blast.ao";
        public const string LineEffect = "maze_detonation_blast_rect.ao";

        /// <summary>The skills that strike with them, whose areas give the sizes.</summary>
        public const string HoleSkill = "GSTwilightOverlordLightningDonut";
        public const string RingSkill = "GSTwilightOverlordCircleMazeDetonation";

        /// <summary>Both blasts fall this long (ms) after their effect (the files' delay; deaths 4.9 to 5.1 s after it, 2026-11-25).</summary>
        public const int BlastMs = 5000;

        /// <summary>A blast is past this long (ms) after it fell: a frame or two late is not early.</summary>
        private const int AfterMs = 250;

        /// <summary>The second effect came 6.4 s after the first (2026-11-25): with none by this long (ms) after it the round is over.</summary>
        private const int RoundMs = 9000;

        /// <summary>A round around the arena's middle raises rings; one drawn farther from it than this (world units; 729 measured) raises lines.</summary>
        private const float MiddleWorld = 200f;

        private long? holeAt;
        private long? secondAt;
        private bool rings;

        /// <summary>Where the round is drawn: its hole's and rings' centre.</summary>
        public Vector2 Centre { get; private set; }

        /// <summary>The second blast's effect is drawn.</summary>
        public bool SecondDrawn => this.secondAt != null;

        public void Reset()
        {
            this.holeAt = null;
            this.secondAt = null;
        }

        /// <summary>An effect the game drew; true when it is the round's.</summary>
        public bool Drawn(string ao, Vector2 at, Vector2? middle, long now)
        {
            switch (ao[(ao.LastIndexOf('/') + 1)..])
            {
                case HoleEffect:
                    (this.holeAt, this.secondAt, this.Centre) = (now, null, at);
                    this.rings = middle is not { } arena || Vector2.Distance(arena, at) <= MiddleWorld;
                    return true;
                case RingEffect:
                    (this.secondAt, this.rings) = (now, true);
                    this.Centre = at == Vector2.Zero ? this.Centre : at;
                    return true;
                case LineEffect:
                    (this.secondAt, this.rings) = (now, false);
                    return true;
                default:
                    return false;
            }
        }

        public MazePhase Phase(long now)
        {
            if (this.secondAt is { } second)
            {
                return now - second <= BlastMs + AfterMs ? this.rings ? MazePhase.Rings : MazePhase.Lines : MazePhase.None;
            }

            return this.holeAt is not { } hole || now - hole > RoundMs ? MazePhase.None
                : now - hole <= BlastMs + AfterMs ? MazePhase.Hole
                : this.rings ? MazePhase.Rings : MazePhase.Lines;
        }
    }
}
