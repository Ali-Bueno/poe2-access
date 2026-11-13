namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Switches and levers the game builds as chests, told apart from breakables by their metadata name.</summary>
    public static class Switches
    {
        /// <summary>A switch or lever (MausoleumSecretDoorSwitch, PrisonLever, the Act 3 water levers) by its own name.</summary>
        public static bool IsSwitch(string path)
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            return name.Contains("Switch", StringComparison.OrdinalIgnoreCase) || name.Contains("Lever", StringComparison.OrdinalIgnoreCase);
        }
    }
}
