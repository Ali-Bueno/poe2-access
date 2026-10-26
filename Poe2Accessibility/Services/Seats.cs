namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The local (couch co-op) players the mod reads for, settings <c>ReadPlayer1</c> and <c>ReadPlayer2</c>
    ///     (docs/research/local-coop.md). Every service that follows "the player" reads <see cref="Followed"/>: player 2
    ///     when only player 2 is read and has joined, else player 1 (GameHelper's <c>AreaInstance.Player</c>). While both
    ///     are read, player 2 is the <see cref="Partner"/>, with a radar of its own.
    /// </summary>
    public static class Seats
    {
        private static LocalPlayers? players;
        private static Func<AccessibilitySettings>? settings;

        /// <summary>The player the mod follows: position, life, level, buffs, skill bar.</summary>
        public static Entity Followed => FollowsSecond && players?.Second() is { } second ? second : First;

        /// <summary>Whether the mod follows player 2: read alone, and joined.</summary>
        public static bool FollowsSecond => settings?.Invoke() is { ReadPlayer1: false, ReadPlayer2: true } && players?.Second() != null;

        /// <summary>Player 2 while both players are read, else null.</summary>
        public static Entity? Partner => settings?.Invoke() is { ReadPlayer1: true, ReadPlayer2: true } ? players?.Second() : null;

        /// <summary>Whose controller and cursor the mod reads, 1 or 2; set every frame by the plugin (<see cref="InputSeat"/>).</summary>
        public static int Input { get; set; } = 1;

        /// <summary>Whether the controller read is player 2's, who has joined: readouts it asks for (vitals, skill bar) are player 2's.</summary>
        public static bool AskingSecond => Input == 2 && players?.Second() != null;

        /// <summary>The player whose controller asks for a readout.</summary>
        public static Entity Asking => AskingSecond && players?.Second() is { } second ? second : First;

        private static Entity First => Core.States.InGameStateObject.CurrentAreaInstance.Player;

        /// <summary>Set when the plugin starts, cleared when it stops.</summary>
        public static void Attach(LocalPlayers? localPlayers, Func<AccessibilitySettings>? current)
        {
            players = localPlayers;
            settings = current;
            Input = 1;
        }

        /// <summary>Local player <paramref name="seat"/>'s entity; player 1's when player 2 has not joined.</summary>
        public static Entity Of(int seat) => seat == 2 && players?.Second() is { } second ? second : First;

        public static bool IsFollowed(Entity entity) => entity.Address == Followed.Address;

        /// <summary>Either local player: they walk together, so the automatic NPC tap leaves both out.</summary>
        public static bool IsLocal(Entity entity) => entity.Address == First.Address || entity.Address == players?.Second()?.Address;
    }
}
