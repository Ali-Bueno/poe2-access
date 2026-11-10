namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    ///     The mod's binding of PRISM's C API, matched by hand to <c>third_party/prism/include/prism.h</c> (PRISM v0.18.3):
    ///     one prism.dll loaded by its path, the calls the mod makes and nothing more. Shared as source by the plugin's
    ///     speech worker and the executable's startup speech; one thread makes every call.
    /// </summary>
    /// <remarks>
    ///     Not Prismatoid, the official .NET binding: its newest release (0.4.0) carries PRISM 0.17.3, whose Windows build
    ///     has no Wine bridges (no speech under Proton), and it sizes PrismConfig for that release (48 bytes) while 0.18.3's
    ///     prism_config_init writes 56. When prism.dll is upgraded, compare <see cref="PrismConfig"/> and the signatures
    ///     below with the new header (docs/proton.md, "Upgrading PRISM"); a test compares the struct with the header.
    /// </remarks>
    internal sealed unsafe class PrismLibrary : IDisposable
    {
        public const string FileName = "prism.dll";

        /// <summary>The folder beside prism.dll holding the Unix halves of PRISM's Wine bridges; Wine finds them through WINEDLLPATH.</summary>
        public const string WineFolder = "wine";

        /// <summary>The script beside Poe2Access.exe that Steam's launch option runs under Proton (tools/package, docs/proton.md).</summary>
        public const string ProtonScript = "poe2access-proton.sh";

        /// <summary>PRISM_CONFIG_VERSION of the matched header.</summary>
        public const byte ConfigVersion = 4;

        /// <summary>Room given to prism_config_init: the matched struct and spare, so a later release's larger config cannot write past it.</summary>
        public const int ConfigSlotBytes = 256;

        /// <summary>The Wine bridges' placeholders, beside prism.dll; each has a <c>.dll.so</c> of the same name in <see cref="WineFolder"/>.</summary>
        public static readonly string[] WineBridges = { "prism_orca_bridge.dll", "prism_speech_dispatcher_bridge.dll", "prism_spiel_bridge.dll" };

        /// <summary>PRISM_BACKEND_ORCA, PRISM_BACKEND_SPEECH_DISPATCHER and PRISM_BACKEND_SPIEL: the backends behind the Wine bridges.</summary>
        public const ulong OrcaId = 0x10AA1FC05A17F96C;
        public const ulong SpeechDispatcherId = 0xE3D6F895D949EBFE;
        public const ulong SpielId = 0x478B44F14AD3D89C;

        private const int Ok = 0; // PRISM_OK
        private const int ErrorAlreadyInitialized = 15; // PRISM_ERROR_ALREADY_INITIALIZED: acquire_best hands out a started backend since v0.18
        private const ulong SupportedAtRuntime = 1; // PRISM_BACKEND_IS_SUPPORTED_AT_RUNTIME
        private const int LogLevelDebug = 1; // PRISM_LOG_LEVEL_DEBUG
        private const int LogLevelWarn = 3; // PRISM_LOG_LEVEL_WARN

        /// <summary>Where PRISM's own log lines go under Wine (set by <see cref="Start"/>); PRISM may call from its own threads.</summary>
        private static volatile Action<string>? prismLog;

        private readonly string path;
        private readonly IntPtr library;
        private readonly delegate* unmanaged[Cdecl]<ConfigSlot> configInit;
        private readonly delegate* unmanaged[Cdecl]<PrismConfig*, IntPtr> init;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, void> shutdown;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, nuint> registryCount;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, nuint, ulong> registryIdAt;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, ulong, byte*> registryName;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, ulong, int> registryPriority;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, IntPtr> acquireBest;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, void> backendFree;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*> backendName;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, int> backendInitialize;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*, byte, int> backendSpeak;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, int> backendStop;
        private readonly delegate* unmanaged[Cdecl]<int, byte*> errorString;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr> registryGet;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr> registryAcquire;
        private readonly delegate* unmanaged[Cdecl]<IntPtr, ulong> backendGetFeatures;
        private readonly delegate* unmanaged[Cdecl]<LogHandler, LogHandler> setLogHandler;
        private readonly delegate* unmanaged[Cdecl]<int, int> setLogLevel;
        private IntPtr context;
        private IntPtr backend;

        private PrismLibrary(string path, IntPtr library)
        {
            this.path = path;
            this.library = library;
            this.configInit = (delegate* unmanaged[Cdecl]<ConfigSlot>)this.Export("prism_config_init");
            this.init = (delegate* unmanaged[Cdecl]<PrismConfig*, IntPtr>)this.Export("prism_init");
            this.shutdown = (delegate* unmanaged[Cdecl]<IntPtr, void>)this.Export("prism_shutdown");
            this.registryCount = (delegate* unmanaged[Cdecl]<IntPtr, nuint>)this.Export("prism_registry_count");
            this.registryIdAt = (delegate* unmanaged[Cdecl]<IntPtr, nuint, ulong>)this.Export("prism_registry_id_at");
            this.registryName = (delegate* unmanaged[Cdecl]<IntPtr, ulong, byte*>)this.Export("prism_registry_name");
            this.registryPriority = (delegate* unmanaged[Cdecl]<IntPtr, ulong, int>)this.Export("prism_registry_priority");
            this.acquireBest = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)this.Export("prism_registry_acquire_best");
            this.backendFree = (delegate* unmanaged[Cdecl]<IntPtr, void>)this.Export("prism_backend_free");
            this.backendName = (delegate* unmanaged[Cdecl]<IntPtr, byte*>)this.Export("prism_backend_name");
            this.backendInitialize = (delegate* unmanaged[Cdecl]<IntPtr, int>)this.Export("prism_backend_initialize");
            this.backendSpeak = (delegate* unmanaged[Cdecl]<IntPtr, byte*, byte, int>)this.Export("prism_backend_speak");
            this.backendStop = (delegate* unmanaged[Cdecl]<IntPtr, int>)this.Export("prism_backend_stop");
            this.errorString = (delegate* unmanaged[Cdecl]<int, byte*>)this.Export("prism_error_string");
            this.registryGet = (delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr>)this.Export("prism_registry_get");
            this.registryAcquire = (delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr>)this.Export("prism_registry_acquire");
            this.backendGetFeatures = (delegate* unmanaged[Cdecl]<IntPtr, ulong>)this.Export("prism_backend_get_features");
            this.setLogHandler = (delegate* unmanaged[Cdecl]<LogHandler, LogHandler>)this.Export("prism_set_log_handler");
            this.setLogLevel = (delegate* unmanaged[Cdecl]<int, int>)this.Export("prism_set_log_level");
            this.Version = NativeLibrary.TryGetExport(library, "prism_version_string", out var version)
                ? Text(((delegate* unmanaged[Cdecl]<byte*>)version)()) ?? "?"
                : "?";
        }

        /// <summary>The library's own version string ("0.18.3").</summary>
        public string Version { get; }

        /// <summary>The backend <see cref="Start"/> took ("NVDA", "Speech Dispatcher"), or empty.</summary>
        public string BackendName { get; private set; } = string.Empty;

        /// <summary>The config version the library's prism_config_init wrote: <see cref="ConfigVersion"/> unless the dll is another release's.</summary>
        public byte LibraryConfigVersion { get; private set; }

        /// <summary>Loads prism.dll by its path; throws DllNotFoundException, BadImageFormatException or EntryPointNotFoundException.</summary>
        public static PrismLibrary Load(string path)
        {
            var full = Path.GetFullPath(path);
            var handle = NativeLibrary.Load(full);
            try
            {
                return new PrismLibrary(full, handle);
            }
            catch (EntryPointNotFoundException)
            {
                NativeLibrary.Free(handle); // nothing of it ran yet
                throw;
            }
        }

        /// <summary>
        ///     Starts PRISM and takes its best backend: null when it speaks, else why not (under Wine, what the player must
        ///     do). Under Wine only a bridged backend is taken (<see cref="StartBridged"/>), and PRISM's own log goes to
        ///     <paramref name="log"/>: only there does it say why a bridge did not load.
        /// </summary>
        public string? Start(Action<string>? log = null)
        {
            var wine = WineVersion() is not null;
            if (wine && log is not null)
            {
                prismLog = log;
                this.setLogHandler(new LogHandler { Fn = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, int, byte*, byte*, void>)&OnPrismLog });
                this.setLogLevel(LogLevelDebug); // a failed bridge load is logged at debug (v0.18.3)
            }

            var slot = this.configInit();
            this.LibraryConfigVersion = slot.Config.Version;
            this.context = this.init(&slot.Config); // read during the call, not kept
            if (this.context == IntPtr.Zero)
            {
                return $"prism_init refused its own config (version {slot.Config.Version})";
            }

            if (wine)
            {
                var failure = this.StartBridged();
                if (log is not null)
                {
                    this.setLogLevel(LogLevelWarn); // the start's account is kept; per-phrase debug lines are not
                }

                return failure;
            }

            this.backend = this.acquireBest(this.context);
            if (this.backend == IntPtr.Zero)
            {
                return "no speech backend" + (WineHint(this.path) ?? " (is NVDA or JAWS running?)");
            }

            this.BackendName = Text(this.backendName(this.backend)) ?? "?";
            var error = this.backendInitialize(this.backend);
            if (error != Ok && error != ErrorAlreadyInitialized)
            {
                this.backendFree(this.backend);
                this.backend = IntPtr.Zero;
                return $"backend {this.BackendName} did not start: {this.ErrorText(error)}";
            }

            return null;
        }

        /// <summary>Every backend PRISM knows, by priority as it lists them; empty before <see cref="Start"/>.</summary>
        public IReadOnlyList<(string Name, int Priority)> Backends() => this.Registered().Select(entry => (entry.Name, entry.Priority)).ToList();

        /// <summary>
        ///     The backends that can speak under Wine, best first: only the bridged ones (Orca, Speech Dispatcher, Spiel), by
        ///     PRISM's priority, the registry's order on a tie. Never SAPI: Proton's SAPI (protontts over eSpeak-NG) reports a
        ///     voice but the Steam runtime has no eSpeak-NG data, so it is mute while looking started; and at 0.18.3 it ties
        ///     Speech Dispatcher at 97 ahead of it, so acquire_best took it whenever Orca was missing (the D2R mod's tester
        ///     under Proton, 2026-11-09).
        /// </summary>
        public static IEnumerable<ulong> WineCandidates(IEnumerable<(ulong Id, int Priority)> registered) =>
            registered.Where(entry => entry.Id is OrcaId or SpeechDispatcherId or SpielId).OrderByDescending(entry => entry.Priority).Select(entry => entry.Id);

        private List<(ulong Id, string Name, int Priority)> Registered()
        {
            var backends = new List<(ulong, string, int)>();
            if (this.context == IntPtr.Zero)
            {
                return backends;
            }

            var count = this.registryCount(this.context);
            for (nuint index = 0; index < count; index++)
            {
                var id = this.registryIdAt(this.context, index);
                backends.Add((id, Text(this.registryName(this.context, id)) ?? id.ToString("X16"), this.registryPriority(this.context, id)));
            }

            return backends;
        }

        /// <summary>
        ///     Under Wine: the first bridged backend that is supported here and starts (<see cref="WineCandidates"/>); null when
        ///     one speaks, else why none did, each candidate's reason included.
        /// </summary>
        private string? StartBridged()
        {
            var registered = this.Registered();
            var reasons = new List<string>();
            foreach (var id in WineCandidates(registered.Select(entry => (entry.Id, entry.Priority))))
            {
                var name = registered.First(entry => entry.Id == id).Name;
                var shared = this.registryGet(this.context, id); // the shared instance: probed, never freed
                if (shared == IntPtr.Zero)
                {
                    reasons.Add($"{name} unavailable");
                    continue;
                }

                if ((this.backendGetFeatures(shared) & SupportedAtRuntime) == 0)
                {
                    reasons.Add($"{name} not supported here");
                    continue;
                }

                var acquired = this.registryAcquire(this.context, id);
                if (acquired == IntPtr.Zero)
                {
                    reasons.Add($"{name} not acquired");
                    continue;
                }

                var error = this.backendInitialize(acquired);
                if (error != Ok && error != ErrorAlreadyInitialized)
                {
                    reasons.Add($"{name} did not start: {this.ErrorText(error)}");
                    this.backendFree(acquired);
                    continue;
                }

                this.backend = acquired;
                this.BackendName = Text(this.backendName(acquired)) ?? name;
                return null;
            }

            return "no speech backend" + WineHint(this.path) + (reasons.Count == 0 ? string.Empty : $" ({string.Join("; ", reasons)})");
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void OnPrismLog(IntPtr userdata, int level, byte* source, byte* message)
        {
            try
            {
                prismLog?.Invoke($"PRISM log {level}: {Text(source) ?? "?"}: {Text(message)}");
            }
            catch
            {
                // Nothing may escape into PRISM's thread.
            }
        }

        /// <summary>Speaks through the backend; PRISM's error code (0 when it went out).</summary>
        public int Speak(string text, bool interrupt)
        {
            if (this.backend == IntPtr.Zero)
            {
                return -1;
            }

            fixed (byte* utf8 = Encoding.UTF8.GetBytes(text + '\0'))
            {
                return this.backendSpeak(this.backend, utf8, interrupt ? (byte)1 : (byte)0);
            }
        }

        /// <summary>Silences the backend; PRISM's error code.</summary>
        public int Stop() => this.backend == IntPtr.Zero ? -1 : this.backendStop(this.backend);

        public string ErrorText(int error) => error < 0 ? "no backend" : Text(this.errorString(error)) ?? $"error {error}";

        /// <summary>
        ///     Releases the backend and the context. prism.dll itself stays loaded for the life of the process: the plugin
        ///     loads the same file after the startup speech, and unloading a library whose backends or Wine bridges may
        ///     still run threads could crash the mod.
        /// </summary>
        public void Dispose()
        {
            if (this.backend != IntPtr.Zero)
            {
                this.backendFree(this.backend);
                this.backend = IntPtr.Zero;
            }

            if (this.context != IntPtr.Zero)
            {
                this.shutdown(this.context);
                this.context = IntPtr.Zero;
            }
        }

        /// <summary>Wine's version when this process runs under Wine or Proton (ntdll exports wine_get_version), else null.</summary>
        public static string? WineVersion()
        {
            if (!NativeLibrary.TryLoad("ntdll.dll", out var ntdll))
            {
                return null;
            }

            try
            {
                return NativeLibrary.TryGetExport(ntdll, "wine_get_version", out var export)
                    ? Text(((delegate* unmanaged[Cdecl]<byte*>)export)()) ?? "?"
                    : null;
            }
            finally
            {
                NativeLibrary.Free(ntdll);
            }
        }

        /// <summary>
        ///     Null on Windows. Under Wine, what the player needs for speech: the bridge folder beside prism.dll in WINEDLLPATH
        ///     when the mod starts (Wine reads it then; the mod cannot set it), as the Unix path Wine gives for it.
        /// </summary>
        public static string? WineHint(string prismPath)
        {
            if (WineVersion() is null)
            {
                return null;
            }

            var folder = Path.Join(Path.GetDirectoryName(Path.GetFullPath(prismPath)), WineFolder);
            var seen = Environment.GetEnvironmentVariable("WINEDLLPATH");
            return $" under Wine: start the game from Steam with the launch option sh \"<mod folder>/{ProtonScript}\" %command% (docs/proton.md), "
                + $"which puts \"{UnixPath(folder) ?? folder}\" in WINEDLLPATH, with Orca or Speech Dispatcher running"
                + (string.IsNullOrEmpty(seen) ? "; the mod sees no WINEDLLPATH" : $"; the mod sees WINEDLLPATH={seen}");
        }

        /// <summary>The Unix path Wine maps a Windows path to (kernel32's wine_get_unix_file_name), or null.</summary>
        private static string? UnixPath(string windowsPath)
        {
            if (!NativeLibrary.TryLoad("kernel32.dll", out var kernel32))
            {
                return null;
            }

            try
            {
                if (!NativeLibrary.TryGetExport(kernel32, "wine_get_unix_file_name", out var toUnix)
                    || !NativeLibrary.TryGetExport(kernel32, "GetProcessHeap", out var processHeap)
                    || !NativeLibrary.TryGetExport(kernel32, "HeapFree", out var heapFree))
                {
                    return null;
                }

                fixed (char* name = windowsPath)
                {
                    var unix = ((delegate* unmanaged<char*, byte*>)toUnix)(name);
                    if (unix == null)
                    {
                        return null;
                    }

                    var text = Text(unix);
                    ((delegate* unmanaged<IntPtr, uint, void*, int>)heapFree)(((delegate* unmanaged<IntPtr>)processHeap)(), 0, unix);
                    return text;
                }
            }
            finally
            {
                NativeLibrary.Free(kernel32);
            }
        }

        private static string? Text(byte* utf8) => utf8 == null ? null : Marshal.PtrToStringUTF8((IntPtr)utf8);

        private IntPtr Export(string name) => NativeLibrary.GetExport(this.library, name);

        /// <summary>PrismLogHandler of prism.h v0.18.3: the callback and its userdata, passed and returned by value.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LogHandler
        {
            public IntPtr Fn;
            public IntPtr Userdata;
        }

        /// <summary>PrismConfig of prism.h v0.18.3 (PRISM_CONFIG_VERSION 4), field for field: 56 bytes on x64; a C bool is one byte.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct PrismConfig
        {
            public byte Version;
            public IntPtr Registry;
            public IntPtr AvailabilityCallback;
            public IntPtr AvailabilityUserdata;
            public uint AvailabilityPollIntervalMs;
            public uint AvailabilityDebounceSamples;
            public uint AvailabilityBackoffMaxMs;
            public byte AvailabilityAutoPowerManage;
            public IntPtr AvailabilityBaselineCallback;
        }

        /// <summary>What prism_config_init returns into (through the hidden pointer x64 uses for a struct of this size).</summary>
        [StructLayout(LayoutKind.Explicit, Size = ConfigSlotBytes)]
        internal struct ConfigSlot
        {
            [FieldOffset(0)]
            public PrismConfig Config;
        }
    }
}
