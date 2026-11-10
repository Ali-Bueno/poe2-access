namespace Poe2Access;

using System;

/// <summary>What to do on one look at the wait for the game window.</summary>
internal enum WaitStep
{
    Wait,
    Remind,
    GiveUp,
}

/// <summary>
///     The rule for waiting on the game window: go on while something that could start the game runs (Steam patching or
///     downloading, the game or its patcher loading), give up when nothing has for a whole patience, and say that it
///     waits first after a short time and then at a calm pace.
/// </summary>
internal sealed class GameWait(TimeSpan patience, TimeSpan firstReminder, TimeSpan reminderEvery)
{
    private DateTime lastReason;
    private DateTime nextReminder;

    /// <summary>Starts the clocks from <paramref name="now"/>: the patience counts from here when nothing runs yet.</summary>
    public void Start(DateTime now)
    {
        this.lastReason = now;
        this.nextReminder = now + firstReminder;
    }

    /// <param name="somethingRuns">Whether something that could still bring the game up runs.</param>
    public WaitStep Look(bool somethingRuns, DateTime now)
    {
        if (somethingRuns)
        {
            this.lastReason = now;
        }
        else if (now - this.lastReason >= patience)
        {
            return WaitStep.GiveUp;
        }

        if (now < this.nextReminder)
        {
            return WaitStep.Wait;
        }

        this.nextReminder = now + reminderEvery;
        return WaitStep.Remind;
    }
}
