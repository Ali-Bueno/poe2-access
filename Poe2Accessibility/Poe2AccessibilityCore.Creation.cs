namespace Poe2Accessibility
{
    using System;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Character creation's spoken help and the presses it ignores, of <see cref="Poe2AccessibilityCore"/>.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        private readonly CreationNameGuide creationGuide = new(GameLayout.CreateCharacter.MaxNameLength);

        /// <summary>Said once per visit, so a refused name coming back through the loading screen stays quiet.</summary>
        private bool creationHelpSaid;

        private bool classHelpSaid;
        private bool creationAHeld;
        private bool loginHintSaid;
        private bool filterHintSaid;

        private static CreationSpot CreationSpotOf(string? control) => control switch
        {
            GameLayout.CreateCharacter.NameBox => CreationSpot.Typing,
            GameLayout.CreateCharacter.NameFrame or GameLayout.CreateCharacter.League or GameLayout.CreateCharacter.Continue => CreationSpot.Outside,
            _ => CreationSpot.Elsewhere,
        };

        /// <summary>
        ///     The creation screen's name field takes typing at once and nothing on screen says how to finish: once per
        ///     visit, after the field, how to create the character (user, 2026-10-24).
        /// </summary>
        private void OnCreationFocus(AccessibleUiNode node)
        {
            if (!this.creationHelpSaid && node.Role == UiRole.TextField && this.gameState?.CurrentState == GameStateTypes.CreateCharacterState)
            {
                this.creationHelpSaid = true;
                this.narrator?.SayAfterFocus(Announcements.CreationNameHelp);
            }
        }

        /// <summary>Once per session, after the first focus: buttons shown beside a control, which the hint bar leaves out.</summary>
        private void OnPreGameFocus(AccessibleUiNode node)
        {
            if (this.gameState?.CurrentState == GameStateTypes.LoginState && !this.loginHintSaid)
            {
                this.loginHintSaid = true;
                this.narrator?.SayAfterFocus(Announcements.PatchNotesHint);
            }
            else if (this.gameState?.CurrentState == GameStateTypes.SelectCharacterState && !this.filterHintSaid)
            {
                this.filterHintSaid = true;
                this.narrator?.SayAfterFocus(Announcements.LeagueFilterHint);
            }
        }

        /// <summary>The screen opens with no class highlighted, and A does nothing until the stick has moved (live 2026-11-06).</summary>
        private void SayCreationClassHelp(GameStateTypes current)
        {
            if (current == GameStateTypes.CreateCharacterState && !this.classHelpSaid)
            {
                this.classHelpSaid = true;
                this.output.Speak(Announcements.CreationClassHelp, false);
            }
        }

        /// <summary>Per frame on the creation screen: says why a press did nothing, since the game stays silent.</summary>
        private void TickCreationName()
        {
            if (this.gameState?.CurrentState != GameStateTypes.CreateCharacterState || !Core.Process.Foreground)
            {
                return;
            }

            var node = this.focus?.Current;
            var spot = CreationSpotOf(node?.Name);
            var aDown = this.padState.IsDown(GameLayout.PadButtons.A);
            var presses = spot == CreationSpot.Elsewhere
                ? default
                : new CreationPresses(this.hotkeys.Typed(), spot == CreationSpot.Typing && this.hotkeys.Enter(), aDown && !this.creationAHeld);
            this.creationAHeld = aDown;
            var advice = this.creationGuide.Tick(Environment.TickCount64, spot, node?.Value ?? string.Empty, presses,
                () => this.semantics?.ContinueGrey(this.focus?.CurrentLayer ?? IntPtr.Zero) == true);
            if (advice != CreationAdvice.None)
            {
                // After B the focus line is what the player hears first; everywhere else the press is the news.
                this.output.Speak(Announcements.ForCreation(advice), spot == CreationSpot.Typing || advice != CreationAdvice.NameTooShort);
            }
        }
    }
}
