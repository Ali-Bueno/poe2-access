namespace Poe2Accessibility.Services
{
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The trials' counter lines on the HUD: the Trial of Chaos' round, the Death Crystals left.</summary>
    public sealed partial class ScreenNarrator
    {
        private readonly CounterWatch rounds = new(fallsOnly: false);
        private readonly CounterWatch crystals = new(fallsOnly: true);

        /// <summary>The HUD formats (catalog/trials.json).</summary>
        public TrialCatalog Trials { get; set; } = TrialCatalog.Empty;

        /// <summary>Whether the HUD shows a trial under way: the Sekhemas' honour line or a Trial of Chaos round, in the game's words.</summary>
        public bool TrialShown(ScreenSnapshot snapshot)
        {
            var hud = this.inGame ? snapshot.Find(this.HudLayer) : null;
            return hud != null && (this.HonourLine(snapshot) != null ||
                                   TrialLines.Read(this.Trials.Pattern(TrialCatalog.RoundLabel), hud.Texts.Select(text => text.Text)) != null);
        }

        /// <summary>Whether the HUD counts the Death Crystals left ("6 Death Crystals remaining"): the room is one of crystals against a clock.</summary>
        public bool CrystalsCounted(ScreenSnapshot snapshot)
        {
            var hud = this.inGame ? snapshot.Find(this.HudLayer) : null;
            return hud != null && TrialLines.Read(this.Trials.Pattern(TrialCatalog.CrystalsLabel), hud.Texts.Select(text => text.Text)) != null;
        }

        /// <summary>Says a round beginning (cutting in: a new fight) and the crystals left as one is disabled: digits alone are no news to the differ.</summary>
        public void CheckTrialLines(ScreenSnapshot snapshot)
        {
            var hud = this.inGame ? snapshot.Find(this.HudLayer) : null;
            var texts = hud?.Texts.Select(text => text.Text).ToList() ?? new();
            if (this.rounds.Heard(TrialLines.Read(this.Trials.Pattern(TrialCatalog.RoundLabel), texts)) is { } round)
            {
                this.Speak("trial round", Announcements.TrialRound(round.Value, round.Of), true);
            }

            if (this.crystals.Heard(TrialLines.Read(this.Trials.Pattern(TrialCatalog.CrystalsLabel), texts)) is { } left)
            {
                this.output.Urge(left.Text); // the game's own line, in its language
            }
        }
    }
}
