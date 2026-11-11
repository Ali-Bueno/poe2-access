namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Services;
    using static Check;

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
            (nameof(UnderWineTheDialogsAskTheLinuxDesktop), UnderWineTheDialogsAskTheLinuxDesktop),
            (nameof(AMentionBeingHeardIsNotSaidAgain), AMentionBeingHeardIsNotSaidAgain),
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
            Equal(
                $"#define PRISM_CONFIG_VERSION {PrismLibrary.ConfigVersion}",
                Regex.Match(header, @"#define PRISM_CONFIG_VERSION \d+").Value,
                "the header's config version is the binding's");
            var body = Regex.Match(header, @"typedef struct \{([^}]*)\} PrismConfig;").Groups[1].Value;
            var fields = Regex.Matches(body, @"(\w[\w ]*?)\s*\*?\s*(\w+);").Select(m => m.Groups[2].Value);
            Equal(
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
            Equal("0 8 16 24 32 36 40 44 48", string.Join(' ', offsets), "x64 offsets (MSVC packing)");
            Equal(56, Marshal.SizeOf<PrismLibrary.PrismConfig>(), "the struct prism_config_init writes");
            Equal(true, Marshal.SizeOf<PrismLibrary.ConfigSlot>() >= 2 * Marshal.SizeOf<PrismLibrary.PrismConfig>(), "room to spare for a grown config");
        }

        /// <summary>Each bridge has its placeholder beside prism.dll and its Unix half in wine/, as the plugin build and the package copy them.</summary>
        private static void TheWineBridgesShipWhole()
        {
            var runtime = Path.Join(PrismFolder(), "runtime");
            Equal(true, File.Exists(Path.Join(runtime, PrismLibrary.FileName)), "prism.dll");
            foreach (var bridge in PrismLibrary.WineBridges)
            {
                Equal(true, File.Exists(Path.Join(runtime, bridge)), bridge);
                Equal(true, File.Exists(Path.Join(runtime, PrismLibrary.WineFolder, bridge + ".so")), bridge + ".so");
            }

            var shipped = Directory.EnumerateFiles(runtime, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(runtime, file).Replace('\\', '/')).OrderBy(name => name, StringComparer.Ordinal);
            Equal(7, shipped.Count(), "prism.dll and three bridges in two halves: " + string.Join(", ", shipped));
        }

        /// <summary>The bridged backends' ids are the header's PRISM_BACKEND_ORCA, _SPEECH_DISPATCHER and _SPIEL.</summary>
        private static void TheBridgedBackendIdsAreTheHeaders()
        {
            var header = File.ReadAllText(Path.Join(PrismFolder(), "include", "prism.h"));
            foreach (var (name, id) in new[] { ("ORCA", PrismLibrary.OrcaId), ("SPEECH_DISPATCHER", PrismLibrary.SpeechDispatcherId), ("SPIEL", PrismLibrary.SpielId) })
            {
                var value = Regex.Match(header, $@"#define PRISM_BACKEND_{name} UINT64_C\(0x([0-9A-Fa-f]+)\)").Groups[1].Value;
                Equal(id.ToString("X16"), value.ToUpperInvariant(), "PRISM_BACKEND_" + name);
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
            Equal(
                string.Join(' ', new[] { PrismLibrary.OrcaId, PrismLibrary.SpeechDispatcherId, PrismLibrary.SpielId }),
                string.Join(' ', PrismLibrary.WineCandidates(registry)),
                "Orca, then Speech Dispatcher, then Spiel");
            Equal(
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
            var package = Path.Join(PrismFolder(), "..", "..", "tools", "package");
            foreach (var name in new[] { PrismLibrary.ProtonScript, "poe2access-stop.sh", "install-linux.sh" })
            {
                var text = File.ReadAllText(Path.Join(package, name));
                Equal(false, text.Contains('\r'), $"{name}: no carriage returns");
                Equal(true, text.StartsWith("#!/bin/sh\n", StringComparison.Ordinal), $"{name}: a POSIX sh script");
            }

            var script = File.ReadAllText(Path.Join(package, PrismLibrary.ProtonScript));
            Equal(true, script.Contains("app/Plugins/Poe2Accessibility/" + PrismLibrary.WineFolder, StringComparison.Ordinal), "WINEDLLPATH names the packaged bridge folder");
            Equal(true, File.ReadAllText(Path.Join(package, "poe2access-stop.sh")).Contains("poe2access-proton.env", StringComparison.Ordinal), "the stop script reads the session the launch script records");
        }

        /// <summary>
        ///     Under Wine the start-up questions and errors go to zenity or kdialog, which Orca reads, and their exit code
        ///     comes back as MessageBoxW's answer; nothing written means the message box is shown instead.
        /// </summary>
        private static void UnderWineTheDialogsAskTheLinuxDesktop()
        {
            const uint question = 0x4 | 0x20 | 0x10000 | 0x40000; // the updater's MB_YESNO | MB_ICONQUESTION | MB_SETFOREGROUND | MB_TOPMOST
            Equal("question", Poe2Access.HostDialog.Kind(question), "the update question");
            Equal("error", Poe2Access.HostDialog.Kind(0x10), "an error");
            Equal("info", Poe2Access.HostDialog.Kind(0x40), "a note");
            Equal<int?>(6, Poe2Access.HostDialog.Answer(question, "0\n"), "Yes");
            Equal<int?>(7, Poe2Access.HostDialog.Answer(question, "1\n"), "No, or the window closed");
            Equal<int?>(7, Poe2Access.HostDialog.Answer(question, "255\n"), "a failed zenity is no");
            Equal<int?>(1, Poe2Access.HostDialog.Answer(0x10, "0\n"), "OK");
            Equal<int?>(null, Poe2Access.HostDialog.Answer(question, null), "no zenity nor kdialog: the message box");
            Equal<int?>(null, Poe2Access.HostDialog.Answer(question, string.Empty), "nothing written");
            Equal<int?>(null, Poe2Access.HostDialog.Answer(question, "none\n"), "the script found neither: the message box");
            Equal(false, Poe2Access.HostDialog.Script.Contains('\r'), "the script has Unix line ends");
        }

        /// <summary>
        ///     A mentioned warning (life, a flask) sent with nothing ahead of it is being heard: a phrase cutting in soon after
        ///     waits behind it rather than say it again ("Frasco de vida menor, 5 usos" and then "Frasco de vida menor, 5
        ///     usos. Remolino" 270 ms later, user 2026-11-09). Queued behind a line or followed by another, it is said first.
        /// </summary>
        private static void AMentionBeingHeardIsNotSaidAgain()
        {
            var (speech, pending) = Unstarted();
            speech.Mention("Frasco de vida menor, 5 usos");
            speech.Urge("Remolino");
            Equal("Frasco de vida menor, 5 usos/False|Remolino/False", pending(), "heard at once: the next waits behind it");

            (speech, pending) = Unstarted();
            speech.Speak("Vida 50 por ciento. Has recibido daño. Presiona cruceta izquierda para usar un frasco de vida.");
            speech.Mention("Frasco de vida menor, 5 usos");
            speech.Urge("Enfriado");
            Equal("Frasco de vida menor, 5 usos. Enfriado/True", pending(), "behind a long line: said first");

            (speech, pending) = Unstarted();
            speech.Mention("Vida 50 por ciento");
            speech.Speak("Cofre, A");
            speech.Urge("Enfriado");
            Equal("Vida 50 por ciento. Enfriado/True", pending(), "a line after it would be cut: said first");
        }

        /// <summary>A speech service whose worker never started, and what it has queued ("text/interrupt|…").</summary>
        private static (PrismOutputService Speech, Func<string> Pending) Unstarted()
        {
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            var log = new PluginLog(Path.Join(Path.GetTempPath(), "poe2access-tests"), false);
            var speech = (PrismOutputService)Activator.CreateInstance(typeof(PrismOutputService), Private, null, new object[] { log }, null)!;
            var queue = typeof(PrismOutputService).GetField("pending", Private)!;
            return (speech, () => string.Join('|', ((System.Collections.IEnumerable)queue.GetValue(speech)!).Cast<object>().Select(request =>
            {
                var type = request.GetType();
                return $"{type.GetProperty("Text")!.GetValue(request)}/{type.GetProperty("Interrupt")!.GetValue(request)}";
            })));
        }
    }
}
