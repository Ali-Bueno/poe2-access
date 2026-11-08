using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
///     Plays the game's FMOD Studio events offline through its own <c>fmodstudio.dll</c>: what its sound designers built
///     (layers, pitch, effects), not one raw sample. Only reads the banks; the mix goes to a WAV in the temp folder.
/// </summary>
internal static class EventRenderer
{
    private const string CoreLib = "fmod";
    private const string StudioLib = "fmodstudio";
    private const string CoreFile = "fmod.dll";
    private const string StudioFile = "fmodstudio.dll";

    private const int WavWriterNrt = 5;

    // The plugin's mixer takes this rate and no other (AudioCueService.SampleRate).
    private const int MixRate = 44100;
    private const int DefaultSpeakers = 0;
    private const uint SynchronousUpdate = 0x2;
    private const uint StreamAndMixFromUpdate = 0x3;
    private const int MaxChannels = 256;
    private const int LoadCopy = 0;
    private const int StopImmediate = 1;
    private const int Loaded = 3;
    private const int LoadFailed = 4;
    private const int Stopped = 2;
    private const int PathLength = 1024;

    private const string StringsBank = "Master Bank.strings";
    private const string MasterBank = "Master Bank";
    private const string BankPrefix = "bank:/";

    /// <summary>Long enough for a late attack; an object's cue keeps the first second of it.</summary>
    private const double RenderSeconds = 3;
    private const double GapSeconds = 0.2;
    private const int LoadWaitMilliseconds = 3000;
    private const int LoadPollMilliseconds = 2;

    [DllImport(CoreLib)] private static extern int FMOD_System_SetOutput(IntPtr sys, int output);
    [DllImport(CoreLib)] private static extern int FMOD_System_SetSoftwareFormat(IntPtr sys, int rate, int speakers, int raw);
    [DllImport(CoreLib)] private static extern int FMOD_System_GetMasterChannelGroup(IntPtr sys, out IntPtr group);
    [DllImport(CoreLib)] private static extern int FMOD_ChannelGroup_GetDSPClock(IntPtr group, out ulong clock, out ulong parent);
    [DllImport(CoreLib)] private static extern int FMOD_System_GetSoftwareFormat(IntPtr sys, out int rate, out int mode, out int raw);
    [DllImport(CoreLib)] private static extern int FMOD_System_GetDSPBufferSize(IntPtr sys, out uint length, out int buffers);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_Create(out IntPtr sys, uint headerVersion);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_GetCoreSystem(IntPtr sys, out IntPtr core);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_Initialize(IntPtr sys, int channels, uint studioFlags, uint flags, byte[] wavFile);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_LoadBankMemory(IntPtr sys, byte[] buffer, int length, int mode, uint flags, out IntPtr bank);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_GetEvent(IntPtr sys, byte[] path, out IntPtr description);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_Update(IntPtr sys);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_System_Release(IntPtr sys);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_Bank_GetStringCount(IntPtr bank, out int count);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_Bank_GetStringInfo(IntPtr bank, int index, byte[] guid, byte[] path, int size, out int retrieved);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventDescription_LoadSampleData(IntPtr description);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventDescription_UnloadSampleData(IntPtr description);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventDescription_GetSampleLoadingState(IntPtr description, out int state);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventDescription_CreateInstance(IntPtr description, out IntPtr instance);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventInstance_Start(IntPtr instance);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventInstance_Stop(IntPtr instance, int mode);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventInstance_Release(IntPtr instance);
    [DllImport(StudioLib)] private static extern int FMOD_Studio_EventInstance_GetPlaybackState(IntPtr instance, out int state);

    /// <summary>One mono mix per sound that exists; a sound no event plays is left out.</summary>
    /// <param name="sounds">Each sound with the event paths to try, in order.</param>
    /// <param name="folder">The bank folder of the events (<c>bank:/Chests/</c>'s <c>Chests</c>): the rest stay unread.</param>
    /// <returns>The mixes and their sample rate; null when FMOD would not start.</returns>
    public static (Dictionary<string, float[]> Mixes, int Rate)? Render(string gameDir, uint headerVersion, IReadOnlyDictionary<string, string[]> sounds, string folder)
    {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) => name switch
        {
            CoreLib => NativeLibrary.Load(Path.Combine(gameDir, CoreFile)),
            StudioLib => NativeLibrary.Load(Path.Combine(gameDir, StudioFile)),
            _ => IntPtr.Zero,
        });

        // FMOD takes the file as bytes: a plain name in the temp folder, whatever letters the user's folder has.
        var file = $"poe2-events-{Environment.ProcessId}.wav";
        var previous = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetTempPath();
        var marks = new List<(string Sound, ulong Start, ulong End)>();
        try
        {
            if (FMOD_Studio_System_Create(out var sys, headerVersion) != 0
                || FMOD_Studio_System_GetCoreSystem(sys, out var core) != 0
                || FMOD_System_SetOutput(core, WavWriterNrt) != 0
                || FMOD_System_SetSoftwareFormat(core, MixRate, DefaultSpeakers, 0) != 0
                || FMOD_Studio_System_Initialize(sys, MaxChannels, SynchronousUpdate, StreamAndMixFromUpdate, Bytes(file)) != 0)
            {
                Say.Out.WriteLine("FMOD Studio would not start (header version mismatch?).");
                return null;
            }

            try
            {
                var banks = LoadBanks(sys, gameDir, folder);
                Say.Out.WriteLine($"Loaded {banks} {folder} banks");
                FMOD_System_GetMasterChannelGroup(core, out var master);
                FMOD_System_GetSoftwareFormat(core, out var rate, out _, out _);
                FMOD_System_GetDSPBufferSize(core, out var block, out _);
                var maxBlocks = (int)(RenderSeconds * rate / block);
                var gapBlocks = (int)(GapSeconds * rate / block);
                foreach (var (sound, paths) in sounds)
                {
                    if (paths.Select(path => Find(sys, path)).FirstOrDefault(found => found != IntPtr.Zero) is var description && description == IntPtr.Zero)
                    {
                        Say.Out.WriteLine($"SKIP no event for {sound}");
                        continue;
                    }

                    if (Play(sys, master, description, maxBlocks) is { } played)
                    {
                        marks.Add((sound, played.Start, played.End));
                    }
                    else
                    {
                        Say.Out.WriteLine($"SKIP samples of {sound} would not load");
                    }

                    Update(sys, gapBlocks);
                    FMOD_Studio_EventDescription_UnloadSampleData(description);
                }
            }
            finally
            {
                FMOD_Studio_System_Release(sys);
            }

            return Cut(File.ReadAllBytes(file), marks);
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A temp file left behind harms nothing.
            }

            Environment.CurrentDirectory = previous;
        }
    }

    private static int LoadBanks(IntPtr sys, string gameDir, string folder)
    {
        if (Load(sys, gameDir, StringsBank) is not { } strings || Load(sys, gameDir, MasterBank) is null)
        {
            throw new InvalidDataException("no master banks");
        }

        FMOD_Studio_Bank_GetStringCount(strings, out var count);
        var guid = new byte[16];
        var path = new byte[PathLength];
        var loaded = 0;
        var prefix = $"{BankPrefix}{folder}/";
        for (var i = 0; i < count; i++)
        {
            if (FMOD_Studio_Bank_GetStringInfo(strings, i, guid, path, path.Length, out var length) == 0 && length > 1)
            {
                var name = System.Text.Encoding.UTF8.GetString(path, 0, length - 1);
                if (name.StartsWith(prefix, StringComparison.Ordinal) && Load(sys, gameDir, name[(name.LastIndexOf('/') + 1)..]) != null)
                {
                    loaded++;
                }
            }
        }

        return loaded;
    }

    private static IntPtr? Load(IntPtr sys, string gameDir, string name)
    {
        var data = GameContent.ReadBank(gameDir, name);
        return data != null && FMOD_Studio_System_LoadBankMemory(sys, data, data.Length, LoadCopy, 0, out var bank) == 0 ? bank : null;
    }

    private static IntPtr Find(IntPtr sys, string path) =>
        FMOD_Studio_System_GetEvent(sys, Bytes(path), out var description) == 0 ? description : IntPtr.Zero;

    private static (ulong Start, ulong End)? Play(IntPtr sys, IntPtr master, IntPtr description, int maxBlocks)
    {
        if (FMOD_Studio_EventDescription_LoadSampleData(description) != 0 || !WaitForSamples(sys, description)
            || FMOD_Studio_EventDescription_CreateInstance(description, out var instance) != 0)
        {
            return null;
        }

        FMOD_ChannelGroup_GetDSPClock(master, out var start, out _);
        FMOD_Studio_EventInstance_Start(instance);
        for (var i = 0; i < maxBlocks; i++)
        {
            FMOD_Studio_System_Update(sys);
            if (FMOD_Studio_EventInstance_GetPlaybackState(instance, out var state) != 0 || state == Stopped)
            {
                break;
            }
        }

        FMOD_ChannelGroup_GetDSPClock(master, out var end, out _);
        FMOD_Studio_EventInstance_Stop(instance, StopImmediate);
        FMOD_Studio_EventInstance_Release(instance);
        return (start, end);
    }

    private static bool WaitForSamples(IntPtr sys, IntPtr description)
    {
        for (var waited = 0; waited < LoadWaitMilliseconds; waited += LoadPollMilliseconds)
        {
            FMOD_Studio_EventDescription_GetSampleLoadingState(description, out var state);
            if (state is Loaded or LoadFailed)
            {
                return state == Loaded;
            }

            Thread.Sleep(LoadPollMilliseconds);
            FMOD_Studio_System_Update(sys);
        }

        return false;
    }

    private static void Update(IntPtr sys, int blocks)
    {
        for (var i = 0; i < blocks; i++)
        {
            FMOD_Studio_System_Update(sys);
        }
    }

    /// <summary>Each mark's stretch of the 16-bit WAV FMOD wrote, its channels averaged.</summary>
    private static (Dictionary<string, float[]> Mixes, int Rate)? Cut(byte[] wav, List<(string Sound, ulong Start, ulong End)> marks)
    {
        var format = wav.AsSpan().IndexOf("fmt "u8);
        var data = wav.AsSpan().IndexOf("data"u8);
        if (format < 0 || data < 0 || BitConverter.ToInt16(wav, format + 22) != 16)
        {
            Say.Out.WriteLine("FMOD wrote no 16-bit WAV.");
            return null;
        }

        int channels = BitConverter.ToInt16(wav, format + 10);
        var rate = BitConverter.ToInt32(wav, format + 12);
        var first = data + 8;
        var frames = (wav.Length - first) / (channels * 2);
        var mixes = new Dictionary<string, float[]>();
        foreach (var (sound, start, end) in marks)
        {
            var from = (int)Math.Min(start, (ulong)frames);
            var to = (int)Math.Min(end, (ulong)frames);
            var mix = new float[to - from];
            for (var frame = from; frame < to; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += BitConverter.ToInt16(wav, first + (((frame * channels) + channel) * 2)) / 32768f;
                }

                mix[frame - from] = sum / channels;
            }

            mixes[sound] = mix;
        }

        return (mixes, rate);
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text + '\0');
}
