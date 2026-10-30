namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     One local player's own news: low life and mana, level up, states begun and ended, charges. The plugin keeps one
    ///     for the player it follows and one for the partner while both local players are read (<see cref="Seats"/>).
    /// </summary>
    public sealed class PlayerAlerts
    {
        private readonly Func<Entity?> subject;
        private readonly IAccessibilityOutputService output;

        /// <summary>Half and a quarter; re-armed 10 points above.</summary>
        private readonly VitalAlarm lifeAlarm = new(10, 50, 25);

        /// <summary>
        ///     Half, a quarter and a tenth; re-armed 30 points above: mana is spent and refilled constantly, and at 10 the
        ///     half was said every 8 s through the Miller fight (2026-10-24). Urgent only when nearly gone.
        /// </summary>
        private readonly VitalAlarm manaAlarm = new(30, 50, 25, 10);

        /// <summary>Three quarters, half, a quarter and gone (under 1); re-armed 10 points above.</summary>
        private readonly VitalAlarm shieldAlarm = new(10, 75, 50, 25, EnergyShieldGone);

        private const int EnergyShieldGone = 1;

        /// <summary>From here down the energy shield's news cuts in when urgent.</summary>
        private const int UrgentEnergyShield = 25;

        /// <summary>
        ///     Energy shield news only for a pool at least this share of life: a sliver from one piece of gear (a helmet on a
        ///     warrior) would break in every fight.
        /// </summary>
        private const float EnergyShieldMinShare = 0.2f;

        private (string Character, int Level) lastLevel;

        /// <summary><paramref name="audio"/> null: the charges are only said (the partner's).</summary>
        public PlayerAlerts(Func<Entity?> subject, AudioCueService? audio, IAccessibilityOutputService output, PluginLog log)
        {
            this.subject = subject;
            this.output = output;
            this.Statuses = new StatusWatch(subject, output, log);
            this.Charges = new ChargeWatch(subject, audio, output, log);
        }

        public StatusWatch Statuses { get; }

        public ChargeWatch Charges { get; }

        /// <summary>The player's character name, "" when there is none.</summary>
        public static string Name(Entity? entity) => entity != null && entity.TryGetComponent<Player>(out var player) ? player.Name : string.Empty;

        /// <summary>Per frame in the game state; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(AccessibilitySettings settings, bool playing)
        {
            if (this.subject() is not { } entity)
            {
                return;
            }

            this.WatchLife(entity, settings);
            this.WatchLevel(entity, settings);
            this.Statuses.Tick(playing && settings.StatusAlerts);
            this.Charges.Tick(playing && settings.ChargeAlerts);
        }

        /// <summary>"Level 3" when the character levels up; the first reading of a character is only remembered.</summary>
        private void WatchLevel(Entity entity, AccessibilitySettings settings)
        {
            if (!entity.TryGetComponent<Player>(out var character) || character.Level <= 0)
            {
                return;
            }

            if (this.lastLevel.Character == character.Name && character.Level > this.lastLevel.Level && settings.AnnounceLevelUp)
            {
                this.output.Speak(Announcements.LevelUp(character.Level), false);
            }

            this.lastLevel = (character.Name, character.Level);
        }

        /// <summary>A warning when life, mana or energy shield falls below a threshold, and when the energy shield is gone.</summary>
        private void WatchLife(Entity entity, AccessibilitySettings settings)
        {
            if (!entity.TryGetComponent<Life>(out var life) || !life.IsAlive || life.Health.Total <= 0)
            {
                return;
            }

            if (settings.AnnounceLowLife && this.lifeAlarm.Update(life.Health.CurrentInPercent()) is { } threshold)
            {
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowLife(threshold), threshold <= settings.LowLifeBelow);
            }

            if (settings.AnnounceLowMana && life.Mana.Total > 0 && this.manaAlarm.Update(life.Mana.CurrentInPercent()) is { } manaThreshold)
            {
                this.output.Say(SpeechKind.LifeAndMana, Announcements.LowMana(manaThreshold), manaThreshold <= 10);
            }

            if (settings.AnnounceEnergyShield && life.EnergyShield.Total >= life.Health.Total * EnergyShieldMinShare &&
                this.shieldAlarm.Update(life.EnergyShield.CurrentInPercent()) is { } shieldThreshold)
            {
                this.output.Say(
                    SpeechKind.LifeAndMana,
                    shieldThreshold == EnergyShieldGone ? Announcements.EnergyShieldGone : Announcements.LowEnergyShield(shieldThreshold),
                    shieldThreshold <= UrgentEnergyShield);
            }
        }
    }
}
