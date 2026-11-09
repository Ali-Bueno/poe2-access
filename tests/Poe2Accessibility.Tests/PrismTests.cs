namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Services;

    /// <summary>The PRISM binding against the shipped header and payload, and a dev command that speaks through a built prism.dll.</summary>
    public static class PrismTests
    {
        /// <summary>Dev command: load prism.dll from a folder (the built plugin's), start it, log the backend, say one line.</summary>
        public const string CheckCommand = "prism";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheConfigMatchesTheShippedHeader), TheConfigMatchesTheShippedHeader),
            (nameof(TheWineBridgesShipWhole), TheWineBridgesShipWhole),
            (nameof(TheBridgedBackendIdsAreTheHeaders), TheBridgedBackendIdsAreTheHeaders),
            (nameof(UnderWineOnlyTheBridgedBackendsAreTried), UnderWineOnlyTheBridgedBackendsAreTried),
            (nameof(TheProtonScriptShipsForSh), TheProtonScriptShipsForSh),
        };

        /// <summary>
        ///     Loads, starts, lists, speaks, shuts down, twice over the same loaded library as the executable does (the
        ///     startup speech, then the plugin); exit code 0 when both lines went out.
        /// </summary>
        public static int Check(string folder)
        {
            var path = Path.Join(folder, PrismLibrary.FileName);
            Console.WriteLine($"Loading {Path.GetFullPath(path)}");
            Console.WriteLine($"Wine: {PrismLibrary.WineVersion() ?? "no"}");
            foreach (var round in new[] { "startup speech", "plugin" })
            {
                using var prism = PrismLibrary.Load(path);
                var failure = prism.Start(message => Console.WriteLine(message));
                Console.WriteLine($"[{round}] PRISM {prism.Version}, config version {prism.LibraryConfigVersion} (binding: {PrismLibrary.ConfigVersion})");
                if (round == "startup speech")
                {
                    foreach (var (name, priority) in prism.Backends())
                    {
                        Console.WriteLine($"  registered: {name} (priority {priority})");
                    }
                }

                if (failure != null)
                {
                    Console.WriteLine($"[{round}] no speech: {failure}");
                    return 1;
                }

                var error = prism.Speak($"PRISM {prism.Version}, {round}, backend {prism.BackendName}.", interrupt: true);
                Console.WriteLine($"[{round}] backend {prism.BackendName}, speak: {(error == 0 ? "ok" : prism.ErrorText(error))}");
                System.Threading.Thread.Sleep(3000); // let the line be heard before the backend goes
                if (error != 0)
                {
                    return 1;
                }
            }

            var bridges = System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>()
                .Select(module => module.ModuleName).Where(name => PrismLibrary.WineBridges.Contains(name, StringComparer.OrdinalIgnoreCase));
            Console.WriteLine($"Wine bridges loaded: {string.Join(", ", bridges.DefaultIfEmpty("none"))}");
            return 0;
        }

        private static string PrismFolder()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var prism = Path.Join(folder.FullName, "third_party", "prism");
                if (Directory.Exists(prism))
                {
                    return prism;
                }
            }

            throw new Exception("third_party/prism not found above the test binaries");
        }

        /// <summary>PrismLibrary.PrismConfig is the header's PrismConfig: same fields in the same order, 56 bytes on x64.</summary>
        private static void TheConfigMatchesTheShippedHeader()
        {
            var header = File.ReadAllText(Path.Join(PrismFolder(), "include", "prism.h"));
            LayoutSearchTests.Equal(
                $"#define PRISM_CONFIG_VERSION {PrismLibrary.ConfigVersion}",
                Regex.Match(header, @"#define PRISM_CONFIG_VERSION \d+").Value,
                "the header's config version is the binding's");
            var body = Regex.Match(header, @"typedef struct \{([^}]*)\} PrismConfig;").Groups[1].Value;
            var fields = Regex.Matches(body, @"(\w[\w ]*?)\s*\*?\s*(\w+);").Select(m => m.Groups[2].Value);
            LayoutSearchTests.Equal(
                "version registry availability_callback availability_userdata availability_poll_interval_ms availability_debounce_samples availability_backoff_max_ms availability_auto_power_manage availability_baseline_callback",
                string.Join(' ', fields),
                "the header's fields, which PrismLibrary.PrismConfig mirrors");

            var offsets = new[]
            {
                nameof(PrismLibrary.PrismConfig.Version), nameof(PrismLibrary.PrismConfig.Registry), nameof(PrismLibrary.PrismConfig.AvailabilityCallback),
                nameof(PrismLibrary.PrismConfig.AvailabilityUserdata), nameof(PrismLibrary.PrismConfig.AvailabilityPollIntervalMs),
                nameof(PrismLibrary.PrismConfig.AvailabilityDebounceSamples), nameof(PrismLibrary.PrismConfig.AvailabilityBackoffMaxMs),
                nameof(PrismLibrary.PrismConfig.AvailabilityAutoPowerManage), nameof(PrismLibrary.PrismConfig.AvailabilityBaselineCallback),
            }.Select(field => Marshal.OffsetOf<PrismLibrary.PrismConfig>(field).ToInt32());
            LayoutSearchTests.Equal("0 8 16 24 32 36 40 44 48", string.Join(' ', offsets), "x64 offsets (MSVC packing)");
            LayoutSearchTests.Equal(56, Marshal.SizeOf<PrismLibrary.PrismConfig>(), "the struct prism_config_init writes");
            LayoutSearchTests.Equal(true, Marshal.SizeOf<PrismLibrary.ConfigSlot>() >= 2 * Marshal.SizeOf<PrismLibrary.PrismConfig>(), "room to spare for a grown config");
        }

        /// <summary>Each bridge has its placeholder beside prism.dll and its Unix half in wine/, as the plugin build and the package copy them.</summary>
        private static void TheWineBridgesShipWhole()
        {
            var runtime = Path.Join(PrismFolder(), "runtime");
            LayoutSearchTests.Equal(true, File.Exists(Path.Join(runtime, PrismLibrary.FileName)), "prism.dll");
            foreach (var bridge in PrismLibrary.WineBridges)
            {
                LayoutSearchTests.Equal(true, File.Exists(Path.Join(runtime, bridge)), bridge);
                LayoutSearchTests.Equal(true, File.Exists(Path.Join(runtime, PrismLibrary.WineFolder, bridge + ".so")), bridge + ".so");
            }

            var shipped = Directory.EnumerateFiles(runtime, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(runtime, file).Replace('\\', '/')).OrderBy(name => name, StringComparer.Ordinal);
            LayoutSearchTests.Equal(7, shipped.Count(), "prism.dll and three bridges in two halves: " + string.Join(", ", shipped));
        }

        /// <summary>The bridged backends' ids are the header's PRISM_BACKEND_ORCA, _SPEECH_DISPATCHER and _SPIEL.</summary>
        private static void TheBridgedBackendIdsAreTheHeaders()
        {
            var header = File.ReadAllText(Path.Join(PrismFolder(), "include", "prism.h"));
            foreach (var (name, id) in new[] { ("ORCA", PrismLibrary.OrcaId), ("SPEECH_DISPATCHER", PrismLibrary.SpeechDispatcherId), ("SPIEL", PrismLibrary.SpielId) })
            {
                var value = Regex.Match(header, $@"#define PRISM_BACKEND_{name} UINT64_C\(0x([0-9A-Fa-f]+)\)").Groups[1].Value;
                LayoutSearchTests.Equal(id.ToString("X16"), value.ToUpperInvariant(), "PRISM_BACKEND_" + name);
            }
        }

        /// <summary>
        ///     Under Wine the bridged backends by priority, and never SAPI, which ties Speech Dispatcher at 97 ahead of it in
        ///     0.18.3's registry and is mute under Proton (the D2R mod's tester, 2026-11-09).
        /// </summary>
        private static void UnderWineOnlyTheBridgedBackendsAreTried()
        {
            const ulong nvda = 1, oneCore = 2, sapi = 3, uia = 4;
            var registry = new[]
            {
                (nvda, 103), (PrismLibrary.OrcaId, 100), (oneCore, 98), (sapi, 97), (uia, 97), (PrismLibrary.SpeechDispatcherId, 97), (PrismLibrary.SpielId, 96),
            };
            LayoutSearchTests.Equal(
                string.Join(' ', new[] { PrismLibrary.OrcaId, PrismLibrary.SpeechDispatcherId, PrismLibrary.SpielId }),
                string.Join(' ', PrismLibrary.WineCandidates(registry)),
                "Orca, then Speech Dispatcher, then Spiel");
            LayoutSearchTests.Equal(
                string.Join(' ', new[] { PrismLibrary.SpeechDispatcherId, PrismLibrary.SpielId }),
                string.Join(' ', PrismLibrary.WineCandidates(registry.Where(entry => entry.Item1 != PrismLibrary.OrcaId))),
                "without Orca: Speech Dispatcher, not SAPI");
        }

        /// <summary>
        ///     The launch option's script runs under sh: Unix line ends (a CR breaks every line; .gitattributes keeps it LF
        ///     under core.autocrlf) and the bridge folder the package puts beside prism.dll.
        /// </summary>
        private static void TheProtonScriptShipsForSh()
        {
            var script = File.ReadAllText(Path.Join(PrismFolder(), "..", "..", "tools", "package", PrismLibrary.ProtonScript));
            LayoutSearchTests.Equal(false, script.Contains('\r'), "no carriage returns");
            LayoutSearchTests.Equal(true, script.StartsWith("#!/bin/sh\n", StringComparison.Ordinal), "a POSIX sh script");
            LayoutSearchTests.Equal(true, script.Contains("app/Plugins/Poe2Accessibility/" + PrismLibrary.WineFolder, StringComparison.Ordinal), "WINEDLLPATH names the packaged bridge folder");
        }
    }
}
