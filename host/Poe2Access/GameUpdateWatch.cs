namespace Poe2Access;

using System;

/// <summary>
///     Notices a game patched under a running host. The game data is made when the launcher starts, so a game that
///     restarts patched leaves the plugin on the columns of the old build; the host cannot rebuild them while it runs.
/// </summary>
internal sealed class GameUpdateWatch(string? madeFor)
{
    private bool opened;
    private bool told;

    /// <summary>Whether the install now is not the one the game data was made for; unknown on either side is no change.</summary>
    public static bool Differs(string? madeFor, string? now) => madeFor is not null && now is not null && madeFor != now;

    /// <summary>
    ///     True once, on the first time a new game process is attached (<paramref name="gameOpened"/> turning true) with an
    ///     install that differs from the one the game data was made for. <paramref name="describe"/> reads the files, so it
    ///     runs only on an attach, not every frame.
    /// </summary>
    public bool Poll(bool gameOpened, Func<string?> describe)
    {
        var attached = gameOpened && !this.opened;
        this.opened = gameOpened;
        if (!attached || this.told || !Differs(madeFor, describe()))
        {
            return false;
        }

        this.told = true;
        return true;
    }
}
