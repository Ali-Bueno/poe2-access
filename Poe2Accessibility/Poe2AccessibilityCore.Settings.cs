namespace Poe2Accessibility
{
    using System;
    using ClickableTransparentOverlay.Win32;
    using GameHelper;
    using ImGuiNET;
    using Poe2Accessibility.Services;

    /// <summary>Settings of <see cref="Poe2AccessibilityCore"/>: the host's settings panel, applying them to the services, hotkey checks.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <inheritdoc />
        public override void DrawSettings()
        {
            ImGui.TextWrapped("Blind users: edit config/settings.txt directly (hotkeys by key name); it is reloaded automatically.");
            ImGui.Checkbox("Speech enabled (restart plugin to apply)", ref this.Settings.SpeechEnabled);
            ImGui.Checkbox("Announce game state changes", ref this.Settings.AnnounceGameState);
            ImGui.Checkbox("Announce area changes", ref this.Settings.AnnounceArea);
            ImGui.Checkbox("Read NPC, system, whisper and party chat", ref this.Settings.AnnounceChat);
            ImGui.Checkbox("Also read global and trade chat", ref this.Settings.AnnouncePlayerChat);
            ImGui.Checkbox("Read whole tooltips when the focus lands", ref this.Settings.ReadTooltipsOnFocus);
            ImGui.Checkbox("L3 is the mod button (lines, details, hints, screen)", ref this.Settings.ControllerCommands);
            ImGui.Checkbox("Say when life falls below half and a quarter", ref this.Settings.AnnounceLowLife);
            if (ImGui.Checkbox("Verbose logging", ref this.Settings.VerboseLogging) && this.log != null)
            {
                this.log.Verbose = this.Settings.VerboseLogging;
            }

            ImGui.Checkbox("UI dump: visible elements only", ref this.Settings.UiDumpVisibleOnly);
            ImGui.Text($"Hotkeys: dump UI tree {this.Settings.DumpUiTreeHotKey}, repeat {this.Settings.RepeatLastHotKey}, status {this.Settings.SpeakStatusHotKey}");
            ImGui.Text($"Speech backend: {this.output.BackendName}");
        }

        /// <inheritdoc />
        public override void SaveSettings() => this.settingsStore?.Save(this.Settings);

        private void ApplySettings(AccessibilitySettings settings)
        {
            this.Settings = settings;
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

        private void ApplySoundVolumes(AccessibilitySettings settings)
        {
            static float Fraction(int percent) => Math.Clamp(percent, 0, MaxSoundVolume) / 100f;
            if (this.audio != null)
            {
                this.audio.Volume = Fraction(settings.SoundVolume);
            }

            if (this.radar != null)
            {
                this.radar.Gain = Fraction(settings.RadarVolume);
                this.radar.SmallObstacles = settings.SmallObstacles;
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

            if (this.enemyCues != null)
            {
                this.enemyCues.Gain = Fraction(settings.EnemyVolume);
                this.enemyCues.Reach = settings.MeleeReach;
                this.enemyCues.ReachWorld = Math.Clamp(settings.MeleeReachRange, 0, 500);
            }

            if (this.beacon != null)
            {
                this.beacon.Gain = Fraction(settings.BeaconVolume);
            }

            if (this.bosses != null)
            {
                this.bosses.Gain = Fraction(settings.EnemyVolume);
            }

            if (this.objectSounds != null)
            {
                this.objectSounds.Gain = Fraction(settings.ObjectVolume);
                this.objectSounds.LootLevel = settings.LootSounds;
            }

            if (this.nearby != null)
            {
                this.nearby.LootLevel = settings.LootSounds;
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

        /// <summary>The overlay library's key-timeout table is process-wide: a key the host consumes first never reaches us.</summary>
        private void WarnOnHotkeyCollisions()
        {
            var host = Core.GHSettings;
            var hostKeys = new (string Name, VK Key)[]
            {
                ("disable all rendering", host.DisableAllRenderingKey),
                ("element finder", host.ElementFinderHotKey),
                ("main menu", host.MainMenuHotKey),
            };
            var ours = new (string Name, VK Key)[]
            {
                ("dump UI tree", this.Settings.DumpUiTreeHotKey),
                ("repeat", this.Settings.RepeatLastHotKey),
                ("status", this.Settings.SpeakStatusHotKey),
            };
            foreach (var (ourName, ourKey) in ours)
            {
                foreach (var (hostName, hostKey) in hostKeys)
                {
                    if (ourKey == hostKey)
                    {
                        this.log?.Warn($"Hotkey collision: {ourKey} is used by GameHelper for '{hostName}'; our '{ourName}' will not fire. Change it in config/settings.txt.");
                        this.output.Speak(Announcements.HotkeyCollision(ourKey.ToString(), ourName), false);
                    }
                }
            }
        }
    }
}
