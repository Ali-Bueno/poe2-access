namespace Poe2Access;

using System;

/// <summary>Decides when the next progress line is due: at most one per interval, and only when the count moved.</summary>
internal sealed class ProgressPacer(TimeSpan interval)
{
    private DateTime lastSaid = DateTime.MinValue;
    private int lastCount = -1;

    /// <summary>Whether to say <paramref name="done"/> now; remembers it when so.</summary>
    public bool Due(int done, DateTime now)
    {
        if (done == this.lastCount || now - this.lastSaid < interval)
        {
            return false;
        }

        this.lastSaid = now;
        this.lastCount = done;
        return true;
    }

    /// <summary>Starts the interval from <paramref name="now"/>, so the first progress line follows the opening one by a full interval.</summary>
    public void Start(DateTime now) => this.lastSaid = now;
}
