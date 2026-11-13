namespace Poe2Accessibility
{
    using System;
    using Poe2Accessibility.Services;

    /// <summary>Settings of <see cref="Poe2AccessibilityCore"/>: applying them to the services, hotkey checks.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <inheritdoc />
        public override void SaveSettings() => this.settingsStore?.Save(this.Settings);

        private void ApplySettings(AccessibilitySettings settings)
        {
            this.Settings = settings;
            this.ApplyPassiveTree(settings);

            if (this.log != null)
            {
                this.log.Verbose = settings.VerboseLogging;
                // The settings menu changes the live settings object, so the language is compared with the one applied.
                if (settings.Language != this.appliedLanguage && Localization.PhraseDirectory != null)
                {
                    this.appliedLanguage = settings.Language;
                    Localization.Detect(settings.Language, message => this.log.Info(message));
                }
            }

            if (this.research != null)
            {
                this.research.Enabled = settings.ResearchMode;
            }

            this.ApplySoundVolumes(settings);
        }

        /// <summary>
        ///     The passive tree's settings; applied again once the tree's reader exists, which is made after the settings are loaded
        ///     (until 2026-11-10 a saved finder or guide setting was not applied at start).
        /// </summary>
        private void ApplyPassiveTree(AccessibilitySettings settings)
        {
            if (this.semantics != null)
            {
                this.semantics.PassiveFinderOn = settings.PassiveFinder;
                this.semantics.PassiveGuideBrief = settings.PassiveGuideBrief;
            }
        }

        private void ApplySoundVolumes(AccessibilitySettings settings)
        {
            static float Fraction(int percent) => AudioCueService.Fraction(percent);
            if (this.audio != null)
            {
                this.audio.Volume = Fraction(settings.SoundVolume);
                this.audio.SetLevels(settings.SoundLevels);
            }

            foreach (var radar in new[] { this.radar, this.partnerRadar })
            {
                if (radar != null)
                {
                    radar.Gain = Fraction(settings.RadarVolume);
                    radar.SmallObstacles = settings.SmallObstacles;
                    radar.Rooms = settings.RadarRooms;
                }
            }

            if (this.wallBump != null)
            {
                this.wallBump.Gain = Fraction(settings.RadarVolume);
            }

            if (this.wallTones != null)
            {
                this.wallTones.Gain = Fraction(settings.RadarVolume);
                this.wallTones.RangeWorld = Math.Clamp(settings.WallToneRange, 100, 3000);
            }

            var reach = Math.Clamp(settings.MeleeReachRange, 0, 500);
            if (this.enemyCues != null)
            {
                this.enemyCues.Gain = Fraction(settings.EnemyVolume);
                this.enemyCues.Reach = settings.MeleeReach;
                this.enemyCues.ReachWorld = reach;
            }

            if (this.beacon != null)
            {
                this.beacon.Gain = Fraction(settings.BeaconVolume);
            }

            if (this.bosses != null)
            {
                this.bosses.Gain = Fraction(settings.DodgeVolume);
                this.bosses.ParryLeadMs = settings.ParryLeadMs;
            }

            if (this.hazards != null)
            {
                this.hazards.Gain = Fraction(settings.EnemyVolume);
            }

            if (this.culls != null)
            {
                this.culls.Gain = Fraction(settings.EnemyVolume);
                this.culls.Mode = settings.CullCue;
            }

            if (this.traps != null)
            {
                this.traps.Gain = Fraction(settings.DodgeVolume);
            }

            if (this.deployed != null)
            {
                this.deployed.Gain = Fraction(settings.ObjectVolume);
            }

            if (this.enemyStates != null)
            {
                this.enemyStates.Gain = Fraction(settings.EnemyVolume);
            }

            if (this.weakPoint != null)
            {
                this.weakPoint.Gain = Fraction(settings.EnemyVolume);
                this.weakPoint.ReachWorld = reach;
            }

            if (this.objectSounds != null)
            {
                this.objectSounds.Gain = Fraction(settings.ObjectVolume);
                this.objectSounds.LootLevel = settings.LootSounds;
                this.objectSounds.SoundSet = settings.ItemSounds;
            }

            if (this.lostHint != null)
            {
                this.lostHint.Gain = Fraction(settings.ObjectVolume);
                this.lostHint.LostAfterMs = settings.LostHintSeconds * 1000;
                this.lostHint.SeenGroundAfterMs = settings.UnexploredHintSeconds * 1000;
                this.lostHint.SoundAfterAreaMs = settings.LostHintAreaMinutes * 60000;
                this.lostHint.SpokenAfterAreaMs = settings.LostSpokenHintMinutes * 60000;
            }

            if (this.mapHints != null)
            {
                this.mapHints.SaySide = settings.MapHintSides;
            }

            if (this.nearby != null)
            {
                this.nearby.LootLevel = settings.LootSounds;
            }

            if (this.enemyNews != null)
            {
                this.enemyNews.Directions = settings.EnemyDirections;
            }

            if (this.discoveries != null)
            {
                this.discoveries.Gain = Fraction(settings.ObjectVolume);
            }

            if (this.targets != null)
            {
                this.targets.Gain = Fraction(settings.EnemyVolume);
            }
        }
    }
}
