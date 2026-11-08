namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Whether the stick rule the mod predicts with (<see cref="PassiveStick"/>) is still the game's: each push whose
    ///     landing the game shows is compared with the prediction, and when the misses in the last pushes reach a threshold
    ///     the rule is said lost, once, and predictions stop until the next game process (docs/compatibility.md). Pure.
    /// </summary>
    public sealed class StickCheck
    {
        /// <summary>Pushes judged: the last ten.</summary>
        public const int Window = 10;

        /// <summary>Misses among them that lose the rule: a player's slanted push misses now and then, a changed rule misses most.</summary>
        public const int MaxMisses = 3;

        private readonly Queue<bool> hits = new();

        public int Hits => this.hits.Count(hit => hit);

        public int Misses => this.hits.Count(hit => !hit);

        /// <summary>Gets a value indicating whether the rule has been lost; predictions stop then.</summary>
        public bool Lost { get; private set; }

        /// <summary>Records a push's outcome; true the one time the misses reach the threshold.</summary>
        public bool Record(bool hit)
        {
            if (this.Lost)
            {
                return false;
            }

            this.hits.Enqueue(hit);
            while (this.hits.Count > Window)
            {
                this.hits.Dequeue();
            }

            this.Lost = this.Misses >= MaxMisses;
            return this.Lost;
        }

        /// <summary>A new game process: judged again from nothing.</summary>
        public void Reset()
        {
            this.hits.Clear();
            this.Lost = false;
        }

        public override string ToString() => $"{this.Hits} hit, {this.Misses} missed of the last {this.hits.Count}{(this.Lost ? ", rule lost" : string.Empty)}";
    }
}
