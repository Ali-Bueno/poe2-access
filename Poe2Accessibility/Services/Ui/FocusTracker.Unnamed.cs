namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Models;

    /// <summary>The log line of a focus read without a name, so a window nobody has seen can be fixed from a player's log.</summary>
    public sealed partial class FocusTracker
    {
        private readonly HashSet<IntPtr> loggedUnnamed = new();

        private void NoteUnnamed(IntPtr focused, AccessibleUiNode node)
        {
            if (!UnnamedFocus.Is(node.Text, node.Value, node.Tooltip.Count, node.IsEmptyCell))
            {
                return;
            }

            if (this.loggedUnnamed.Count > UnnamedFocus.MaxLogged)
            {
                this.loggedUnnamed.Clear();
            }

            if (this.loggedUnnamed.Add(focused))
            {
                this.log.Info($"{UnnamedFocus.FocusPrefix} 0x{focused.ToInt64():X} {this.semantics.UnnamedDetails(focused, node.Role, this.CurrentLayer)}");
            }
        }
    }
}
