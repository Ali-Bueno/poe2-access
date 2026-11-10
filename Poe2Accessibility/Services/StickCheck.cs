namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What one judged push did to the rule.</summary>
    public enum StickVerdict
    {
        Same,
        Lost,
        Regained,
    }

    /// <summary>
    ///     Whether the stick rule the mod predicts with (<see cref="PassiveStick"/>) is still the game's: each push whose
    ///     landing the game shows is compared with the prediction. When half the last pushes miss, the rule is said lost, once,
    ///     and predictions stop; pushes are still compared, and when the misses fall back to a few the rule holds again
    ///     (docs/compatibility.md). A changed rule misses most pushes; a slanted push or a popup over the tree misses now and then
    ///     (2026-11-10: 3 of 10 after the attribute popups lost a sound rule for the rest of the session). Pure.
    /// </summary>
    public sealed class StickCheck
    {
        /// <summary>Pushes judged: the last ten.</summary>
        public const int Window = 10;

        /// <summary>Misses among them that lose the rule.</summary>
        public const int MaxMisses = 5;

        /// <summary>Misses among a full window that give a lost rule back.</summary>
        public const int RegainMisses = 2;

        private readonly Queue<bool> hits = new();

        public int Hits => this.hits.Count(hit => hit);

        public int Misses => this.hits.Count(hit => !hit);

        /// <summary>Gets a value indicating whether the rule is lost; predictions stop then.</summary>
        public bool Lost { get; private set; }

        /// <summary>Records a push's outcome; whether the rule was lost or given back by it.</summary>
        public StickVerdict Record(bool hit)
        {
            this.hits.Enqueue(hit);
            while (this.hits.Count > Window)
            {
                this.hits.Dequeue();
            }

            if (!this.Lost && this.Misses >= MaxMisses)
            {
                this.Lost = true;
                return StickVerdict.Lost;
            }

            if (this.Lost && this.hits.Count == Window && this.Misses <= RegainMisses)
            {
                this.Lost = false;
                return StickVerdict.Regained;
            }

            return StickVerdict.Same;
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
