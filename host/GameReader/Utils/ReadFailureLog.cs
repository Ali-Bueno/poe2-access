namespace GameReader.Utils;

using System;

/// <summary>Collapses a burst of failed reads (the game closing) to its first line and a count of the rest.</summary>
/// <param name="quiet">A failure this soon after the last one belongs to the same burst.</param>
/// <param name="longest">A burst older than this speaks again, so a read that stays broken is not silent.</param>
internal sealed class ReadFailureLog(TimeSpan quiet, TimeSpan longest)
{
    private readonly object gate = new();
    private DateTime last = DateTime.MinValue;
    private DateTime started = DateTime.MinValue;
    private int left;

    /// <summary>Notes a failure; true when its line is to be written (the first of a burst).</summary>
    public bool Report(DateTime now)
    {
        lock (this.gate)
        {
            var burst = now - this.last < quiet && now - this.started < longest;
            this.last = now;
            if (burst)
            {
                this.left++;
                return false;
            }

            this.started = now;
            return true;
        }
    }

    /// <summary>The lines left out since the last call, to be said in one line.</summary>
    public int TakeLeftOut()
    {
        lock (this.gate)
        {
            var count = this.left;
            this.left = 0;
            return count;
        }
    }
}
