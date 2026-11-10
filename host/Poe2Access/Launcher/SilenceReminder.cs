namespace Poe2Access;

using System;

/// <summary>Decides when to say that a step still works although its count has not moved: first after a short silence, then at a calm pace.</summary>
internal sealed class SilenceReminder(TimeSpan first, TimeSpan every)
{
    private int lastCount = -1;
    private DateTime movedAt;
    private DateTime next;

    /// <summary>Starts counting the silence from <paramref name="now"/>, with <paramref name="count"/> done.</summary>
    public void Start(int count, DateTime now) => this.Moved(count, now);

    /// <returns>The whole minutes (at least 1) the count has not moved when a reminder is due now, else null.</returns>
    public int? Due(int count, DateTime now)
    {
        if (count != this.lastCount)
        {
            this.Moved(count, now);
            return null;
        }

        if (now < this.next)
        {
            return null;
        }

        this.next = now + every;
        return Math.Max(1, (int)Math.Round((now - this.movedAt).TotalMinutes, MidpointRounding.AwayFromZero));
    }

    private void Moved(int count, DateTime now)
    {
        this.lastCount = count;
        this.movedAt = now;
        this.next = now + first;
    }
}
