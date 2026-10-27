using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Extracts a fixed set of audio-cue samples from the game's FMOD Studio banks
/// (FMOD/Desktop/*.bank = RIFF "FEV " container holding one FSB5 "SND " chunk, all Vorbis).
/// The Vorbis setup headers are referenced by CRC from a library compiled into fmod.dll, so the
/// samples are decoded with the game's own fmod.dll (read-only P/Invoke) and rewritten as 16-bit WAV.
/// </summary>
internal static class SoundExtractor
{
    // name -> (bank file, sample name inside the FSB, max seconds). Sample names come from the FSB5 name table.
    private sealed record Cue(string Name, string Bank, string Sample, double MaxSeconds = 1.5);

    private static readonly Cue[] Cues =
    {
        new("item",       "ItemDrops",         "Item_Drop_Magic_01"),
        new("item_rare",  "ItemDrops",         "Item_Drop_Rare_01"),
        new("currency",   "ItemDrops",         "OrbDrop_Chaos_01"),
        new("gold",       "ItemDrops",         "GoldDrop_01"),
        new("quest_item", "ItemDrops",         "Item_Drop_Quest_01"),
        new("chest",      "Chest_Rarity",      "Chests_Rarity_Magic_Open_01"),
        new("breakable",  "Breakables",        "Breakables_01"),
        new("shrine",     "ChallengeShrines",  "Gameplay_Shrines_ChallengeShrine_Activate_Stinger_01"),
        new("ritual",     "RitualEncounter",   "RituaStart_01"),
        new("exit",       "UI_General",        "WorldAreaTransition_02"),
        new("portal",     "PortalScrollPortal", "PortalScroll_Effects_Po_Activate_01"),
        new("waypoint",   "Waypoint4.0",       "WaypointActivate_01"),
        new("checkpoint", "BossCheckpoints",   "BossCheckpoints_Activate_01"),
        new("npc",        "General_Gameplay",  "Quest_UI_Offer_01"),
    };

    private const string Lib = "fmod";
    private const uint FallbackFmodVersion = 0x00020311; // 2.03.11, used when fmod.dll carries no readable version
    private const uint ModeSample = 0x0109;      // CREATESAMPLE | 2D | LOOP_OFF
    private const uint TimeUnitPcmBytes = 4;

    [DllImport(Lib)] private static extern int FMOD_System_Create(out IntPtr sys, uint headerVersion);
    [DllImport(Lib)] private static extern int FMOD_System_SetOutput(IntPtr sys, int output);
    [DllImport(Lib)] private static extern int FMOD_System_Init(IntPtr sys, int maxChannels, uint flags, IntPtr extra);
    [DllImport(Lib)] private static extern int FMOD_System_Release(IntPtr sys);
    [DllImport(Lib, CharSet = CharSet.Ansi)] private static extern int FMOD_System_CreateSound(IntPtr sys, string path, uint mode, IntPtr exinfo, out IntPtr sound);
    [DllImport(Lib)] private static extern int FMOD_Sound_Release(IntPtr sound);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetNumSubSounds(IntPtr sound, out int n);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetSubSound(IntPtr sound, int index, out IntPtr sub);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetName(IntPtr sound, byte[] name, int len);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetFormat(IntPtr sound, out int type, out int format, out int channels, out int bits);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetDefaults(IntPtr sound, out float freq, out int priority);
    [DllImport(Lib)] private static extern int FMOD_Sound_GetLength(IntPtr sound, out uint length, uint unit);
    [DllImport(Lib)] private static extern int FMOD_Sound_Lock(IntPtr sound, uint offset, uint length, out IntPtr p1, out IntPtr p2, out uint l1, out uint l2);
    [DllImport(Lib)] private static extern int FMOD_Sound_Unlock(IntPtr sound, IntPtr p1, IntPtr p2, uint l1, uint l2);

    // FMOD writes its version as 0x00MMmmpp with each part in decimal digits: 2.3.11 -> 0x00020311.
    private static uint HeaderVersion(string fmodPath)
    {
        var parts = System.Diagnostics.FileVersionInfo.GetVersionInfo(fmodPath).ProductVersion?.Split(' ')[0].Split('.');
        if (parts is { Length: >= 3 } && parts.Take(3).All(part => int.TryParse(part, out var n) && n < 100))
        {
            return parts.Take(3).Aggregate(0u, (version, part) => version << 8 | Convert.ToUInt32(part, 16));
        }

        return FallbackFmodVersion;
    }

    public static int Run(string gameDir, string outDir, string itemCatalog)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        string bankDir = Path.Combine(gameDir, "FMOD", "Desktop");
        string fmodPath = Path.Combine(gameDir, "fmod.dll");
        if (!File.Exists(fmodPath) || !Directory.Exists(bankDir))
        {
            Console.WriteLine($"Not a PoE2 install (need fmod.dll and FMOD\\Desktop): {gameDir}");
            return 1;
        }

        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(),
            (name, _, _) => name == Lib ? NativeLibrary.Load(fmodPath) : IntPtr.Zero);

        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "items"));
        // Every drop sample the item catalog (Poe2UiExtract item-sounds) points to, extracted as items/<sample>.wav.
        var cues = Cues.Concat(ItemSoundExtractor.ReadSamples(itemCatalog)
            .Select(sample => new Cue("items/" + sample, ItemSoundExtractor.Bank, sample))).ToArray();
        string tmp = Path.Combine(Path.GetTempPath(), "poe2-sounds-" + Environment.ProcessId + ".fsb");

        if (FMOD_System_Create(out var sys, HeaderVersion(fmodPath)) != 0 || FMOD_System_SetOutput(sys, 2 /*NOSOUND*/) != 0
            || FMOD_System_Init(sys, 8, 0, IntPtr.Zero) != 0)
        {
            Console.WriteLine("FMOD init failed (header version mismatch).");
            return 1;
        }

        var manifest = new List<string>();
        int ok = 0;
        try
        {
            foreach (var group in cues.GroupBy(c => c.Bank))
            {
                string bankPath = Path.Combine(bankDir, group.Key + ".bank");
                if (!File.Exists(bankPath)) { Console.WriteLine($"SKIP bank missing: {group.Key}"); continue; }

                byte[] bank = File.ReadAllBytes(bankPath);
                int fsb = bank.AsSpan().IndexOf("FSB5"u8);
                if (fsb < 0) { Console.WriteLine($"SKIP no FSB5 in {group.Key}"); continue; }
                File.WriteAllBytes(tmp, bank.AsSpan(fsb).ToArray());

                if (FMOD_System_CreateSound(sys, tmp, ModeSample, IntPtr.Zero, out var snd) != 0)
                { Console.WriteLine($"SKIP cannot open FSB of {group.Key}"); continue; }

                FMOD_Sound_GetNumSubSounds(snd, out int count);
                foreach (var cue in group)
                {
                    IntPtr sub = FindSub(snd, count, cue.Sample);
                    if (sub == IntPtr.Zero) { Console.WriteLine($"SKIP {cue.Name}: sample '{cue.Sample}' not in {group.Key}"); continue; }

                    var (pcm, ch, rate) = Decode(sub);
                    (pcm, double secs) = TrimAndCap(pcm, ch, rate, cue.MaxSeconds);
                    WriteWav(Path.Combine(outDir, cue.Name + ".wav"), pcm, ch, rate);
                    manifest.Add($"{cue.Name}.wav\tFMOD/Desktop/{group.Key}.bank\t{cue.Sample}\t{secs:F2}s\t{ch}ch {rate}Hz");
                    Console.WriteLine($"{cue.Name,-11} <- {group.Key}/{cue.Sample}  {secs:F2}s");
                    ok++;
                }
                FMOD_Sound_Release(snd);
            }
        }
        finally
        {
            FMOD_System_Release(sys);
            try { File.Delete(tmp); } catch { /* best effort */ }
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.txt"),
            "# name\tsource bank\tsample name\tduration\tformat (generated by Poe2UiExtract sounds; local copy, do not commit)\n"
            + string.Join("\n", manifest) + "\n");
        Console.WriteLine($"Wrote {ok}/{cues.Length} cues to {outDir}");
        return ok > 0 ? 0 : 1;
    }

    private static IntPtr FindSub(IntPtr snd, int count, string sample)
    {
        var buf = new byte[256];
        for (int i = 0; i < count; i++)
        {
            if (FMOD_Sound_GetSubSound(snd, i, out var sub) != 0 || sub == IntPtr.Zero) continue;
            if (FMOD_Sound_GetName(sub, buf, buf.Length) != 0) continue;
            int n = Array.IndexOf(buf, (byte)0);
            if (Encoding.UTF8.GetString(buf, 0, n < 0 ? buf.Length : n) == sample) return sub;
        }
        return IntPtr.Zero;
    }

    /// <summary>Decodes a sample to interleaved float PCM.</summary>
    private static (float[] pcm, int channels, int rate) Decode(IntPtr sub)
    {
        FMOD_Sound_GetFormat(sub, out _, out int format, out int ch, out int bits);
        FMOD_Sound_GetDefaults(sub, out float freq, out _);
        FMOD_Sound_GetLength(sub, out uint len, TimeUnitPcmBytes);
        if (format != 5 /*PCMFLOAT*/ && format != 2 /*PCM16*/)
            throw new NotSupportedException($"Unexpected decoded format {format}/{bits} bits");
        FMOD_Sound_Lock(sub, 0, len, out var p1, out var p2, out uint l1, out uint l2);
        var raw = new byte[l1];
        Marshal.Copy(p1, raw, 0, (int)l1);
        FMOD_Sound_Unlock(sub, p1, p2, l1, l2);

        float[] pcm;
        if (format == 5)
        {
            pcm = new float[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, pcm, 0, raw.Length);
        }
        else
        {
            pcm = new float[raw.Length / 2];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = BitConverter.ToInt16(raw, i * 2) / 32768f;
        }
        return (pcm, ch, (int)freq);
    }

    /// <summary>Drops leading silence (below about -50 dBFS), caps the length and fades the tail out.</summary>
    private static (float[] pcm, double seconds) TrimAndCap(float[] pcm, int ch, int rate, double maxSeconds)
    {
        int frames = pcm.Length / ch, start = 0;
        while (start < frames)
        {
            bool loud = false;
            for (int c = 0; c < ch; c++) if (Math.Abs(pcm[start * ch + c]) > 0.003f) { loud = true; break; }
            if (loud) break;
            start++;
        }
        start = Math.Max(0, start - rate / 200); // keep 5 ms of lead-in so the attack is not clipped
        int len = Math.Min(frames - start, (int)(maxSeconds * rate));
        var res = new float[len * ch];
        Array.Copy(pcm, start * ch, res, 0, res.Length);
        int fade = Math.Min(len, rate / 12); // 80 ms
        for (int i = 0; i < fade; i++)
        {
            float g = (float)(fade - i) / fade;
            for (int c = 0; c < ch; c++) res[(len - fade + i) * ch + c] *= g;
        }
        return (res, (double)len / rate);
    }

    private static void WriteWav(string path, float[] pcm, int ch, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        int dataBytes = pcm.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVEfmt "u8);
        w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate);
        w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (float f in pcm) w.Write((short)Math.Clamp(MathF.Round(f * 32767f), -32768f, 32767f));
    }
}
