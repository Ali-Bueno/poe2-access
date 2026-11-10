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
    // A loop is kept whole (the game's own idle beds) and its seam crossfaded, so it repeats without a click. A frozen
    // loop is a one-shot held still: grains of its [FreezeFrom, FreezeTo) seconds layered into a steady bed.
    // Rms, when set, brings a one-shot to that loudness (peaks under PeakCap): a sample far louder than its neighbours.
    private sealed record Cue(string Name, string Bank, string Sample, double MaxSeconds = 1.5, bool Loop = false,
        double FreezeFrom = 0, double FreezeTo = 0, double Rms = 0);

    /// <summary>How the line naming the cues the banks lacked starts; the launcher logs it.</summary>
    public const string MissingPrefix = "MISSING ";

    private const double PeakCap = 0.9;

    private const double LoopMaxSeconds = 20;

    /// <summary>Research dumps keep a sample's first seconds: enough to judge a cue.</summary>
    private const double DumpMaxSeconds = 4;
    private const double LoopSeamSeconds = 0.05;

    // Freeze: 120 ms Hann grains every 30 ms (four overlap, a constant sum of 2), from seeded random points, wrapped
    // round a 6 s loop so it has no seam.
    private const double FreezeSeconds = 6;
    private const double GrainSeconds = 0.12;
    private const int GrainOverlap = 4;
    private const int FreezeSeed = 27;

    private static readonly Cue[] Cues =
    {
        new("item",       "ItemDrops",         "Item_Drop_Magic_01"),
        new("item_rare",  "ItemDrops",         "Item_Drop_Rare_01"),
        new("item_unique", "ItemDrops",        "Items_UniqueDrop_01"),
        new("currency",   "ItemDrops",         "OrbDrop_Chaos_01"),
        new("gold",       "ItemDrops",         "GoldDrop_01"),
        new("quest_item", "ItemDrops",         "Item_Drop_Quest_01"),
        new("chest",      "Chest_Rarity",      "Chests_Rarity_Magic_Open_01"),
        // A body searched for loot (CorpseChests, the Ogham village's victims): the corpse chests' own rummage.
        new("corpse",     "Corpses",           "Open_04"),
        new("breakable",  "Breakables",        "Breakables_01"),
        // Per-kind break sounds, softer than the generic one. Each sample is the one named after the object's own sound
        // event (the .ao SoundEvents of Metadata/Chests/<folder>), the quietest-sounding of its set.
        new("breakables/barrel",  "BarrelChest",             "Break_4",                        1.0),
        new("breakables/pot",     "HiddenPassagePots",       "PotBreak_3",                     1.0),
        new("breakables/bigpot",  "TitanValleyPots",         "Break_LRG_1",                    1.0),
        new("breakables/urn",     "HiddenPassageUrns",       "Urn_7",                          1.0),
        new("breakables/boulder", "DirtyBoulders",           "DirtyBoulder_2_Open_1",          1.0),
        new("breakables/rubble",  "GoblinRockPile",          "VaalRubbleChest_Medium_Cluster_01", 1.0),
        new("breakables/statue",  "EzomyteChests_4.0",       "EzomyteChestMedium_02",          1.0),
        new("breakables/egg",     "EggChest",                "Chest_SirenEgg_05",              1.0),
        new("breakables/bone",    "MudBones_4.0",            "MudBones2_2",                    1.0),
        new("breakables/crate",   "G4_5_1_SmashableClutter", "Environment_G4-5-1_SmashableClutter_Wood_Impact_02", 1.0),
        new("breakables/basket",  "CarverTribeWickerBaskets", "Fall_B_3",                      1.0),
        new("shrine",     "ChallengeShrines",  "Gameplay_Shrines_ChallengeShrine_Activate_Stinger_01"),
        new("ritual",     "RitualEncounter",   "RituaStart_01"),
        // An exit: a low rush of air, repeated faster as the player nears it (user's pick 2026-11-02 of seven wind and
        // whirl sounds; the old crossing sound held as a bed was hard to hear beside a checkpoint's).
        new("exit",       "CaptainSandstorm",  "Whoosh_Foley_GoblinGiantBoss_02", Rms: 0.10),
        new("portal",     "PortalScrollPortal", "PortalScroll_Effects_Po_Activate_01"),
        new("waypoint",   "Waypoint4.0",       "WaypointActivate_01"),
        new("checkpoint", "BossCheckpoints",   "BossCheckpoints_Activate_01"),
        // What each is heard to do when used: the stash's lid, the well's water (docs/research/game-audio.md).
        new("stash",      "PlayerStash_4.0",   "Open_1", Rms: 0.10),
        new("well",       "General_Gameplay",  "Wellwell_FX_Well_03", Rms: 0.10),
        new("npc",       "General_Gameplay",  "Quest_UI_Offer_01"),
        new("switch",     "Foley",             "PlayerCharacter_Foley_HandGrabLever_01"),
        // Deployed-object locators: the Monk's Tempest Bell toll when it is struck, and the Frozen Locus crystal as it forms.
        new("tempest_bell", "Four_TempestBell", "TempestBell_FX_OnHit_01", 2.5),
        new("ice_crystal",  "Four_FrozenLocus", "FrozenLocus_FX_Appear_01", 2.5),
        // A power charge gained: an electric charge cosmetic's gain sound, the user's pick of the game's (2026-10-30).
        new("power_charge", "Four_ElectroChargesMTX", "ChargeGain_Fork_01"),
        new("loops/portal",     "PortalScrollPortal", "PortalScroll_Effects_Po_IdleActive_01", LoopMaxSeconds, true),
        new("loops/waypoint",   "Waypoint4.0",        "WaypointActive_01", LoopMaxSeconds, true),
        new("loops/checkpoint", "BossCheckpoints",    "BossCheckpoints_Loop_01", LoopMaxSeconds, true),
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
    internal static uint HeaderVersion(string fmodPath)
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
        if (OpenFmod(gameDir) is not { } sys)
        {
            return 1;
        }

        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "items"));
        Directory.CreateDirectory(Path.Combine(outDir, "loops"));

        // A loop wins over its cue, so one no longer extracted must not outlive a run into the same folder.
        foreach (var loop in Directory.GetFiles(Path.Combine(outDir, "loops"), "*.wav"))
        {
            if (!Cues.Any(cue => cue.Name == "loops/" + Path.GetFileNameWithoutExtension(loop)))
            {
                File.Delete(loop);
            }
        }

        // Every drop sample the item catalog (Poe2UiExtract item-sounds) points to, extracted as items/<sample>.wav.
        var cues = Cues.Concat(ItemSoundExtractor.ReadSamples(itemCatalog)
            .Select(sample => new Cue("items/" + sample, ItemSoundExtractor.Bank, sample))).ToArray();
        string tmp = TempFsb();
        var manifest = new List<string>();
        var missing = new List<string>();
        int ok = 0;
        try
        {
            foreach (var group in cues.GroupBy(c => c.Bank))
            {
                if (OpenBank(sys, gameDir, group.Key, tmp) is not { } snd)
                {
                    missing.AddRange(group.Select(cue => $"{cue.Name} (bank {group.Key} missing)"));
                    continue;
                }

                FMOD_Sound_GetNumSubSounds(snd, out int count);
                foreach (var cue in group)
                {
                    IntPtr sub = FindSub(snd, count, cue.Sample);
                    if (sub == IntPtr.Zero)
                    {
                        Say.Out.WriteLine($"SKIP {cue.Name}: sample '{cue.Sample}' not in {group.Key}");
                        missing.Add($"{cue.Name} (sample {cue.Sample} not in bank {group.Key})");
                        continue;
                    }


                    var (pcm, ch, rate) = Decode(sub);
                    (pcm, double secs) = !cue.Loop ? TrimAndCap(pcm, ch, rate, cue.MaxSeconds)
                        : cue.FreezeTo > cue.FreezeFrom ? Freeze(pcm, ch, rate, cue.FreezeFrom, cue.FreezeTo)
                        : Seamless(pcm, ch, rate, cue.MaxSeconds);
                    if (cue.Rms > 0)
                    {
                        pcm = Level(pcm, cue.Rms);
                    }

                    var wavPath = Path.Combine(outDir, cue.Name + ".wav");
                    Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
                    WriteWav(wavPath, pcm, ch, rate);
                    manifest.Add($"{cue.Name}.wav\tFMOD/Desktop/{group.Key}.bank\t{cue.Sample}\t{secs:F2}s\t{ch}ch {rate}Hz");
                    Say.Out.WriteLine($"{cue.Name,-11} <- {group.Key}/{cue.Sample}  {secs:F2}s");
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

        // The cue list is what the plugin plays: a folder short of it replaces a complete one with silence, so no manifest is
        // written and the step fails, naming what is missing. The caller extracts beside the real folder and swaps on success.
        if (missing.Count > 0 || ok < cues.Length)
        {
            Say.Out.WriteLine($"{MissingPrefix}{missing.Count} of {cues.Length} cues: {string.Join("; ", missing)}");
            return 1;
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.txt"),
            "# name\tsource bank\tsample name\tduration\tformat (generated by Poe2UiExtract sounds; local copy, do not commit)\n"
            + string.Join("\n", manifest) + "\n");
        Say.Out.WriteLine($"Wrote {ok}/{cues.Length} cues to {outDir}");
        return 0;
    }

    /// <summary>
    /// Research aid: every sample of <paramref name="banks"/> whose name holds <paramref name="filter"/>, written to
    /// <paramref name="outDir"/> as "bank__sample.wav" (trimmed, capped) with its length and peak printed.
    /// </summary>
    public static int DumpBanks(string gameDir, string outDir, string[] banks, string filter)
    {
        if (OpenFmod(gameDir) is not { } sys)
        {
            return 1;
        }

        Directory.CreateDirectory(outDir);
        string tmp = TempFsb();
        int ok = 0;
        try
        {
            foreach (var name in banks)
            {
                if (OpenBank(sys, gameDir, name, tmp) is not { } snd) continue;

                FMOD_Sound_GetNumSubSounds(snd, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (FMOD_Sound_GetSubSound(snd, i, out var sub) != 0 || sub == IntPtr.Zero) continue;
                    string sample = SampleName(sub);
                    if (!sample.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                    var (pcm, ch, rate) = Decode(sub);
                    (pcm, double secs) = TrimAndCap(pcm, ch, rate, DumpMaxSeconds);
                    float peak = pcm.Length == 0 ? 0 : pcm.Max(Math.Abs);
                    string file = string.Concat($"{name}__{sample}".Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                    WriteWav(Path.Combine(outDir, file + ".wav"), pcm, ch, rate);
                    Say.Out.WriteLine($"{name}/{sample}\t{secs:F2}s\tpeak {peak:F2}\t{ch}ch");
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

        Say.Out.WriteLine($"Wrote {ok} samples to {outDir}");
        return ok > 0 ? 0 : 1;
    }

    /// <summary>The game's fmod.dll loaded and an FMOD system without output; null (reason printed) when it fails.</summary>
    private static IntPtr? OpenFmod(string gameDir)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        string fmodPath = Path.Combine(gameDir, "fmod.dll");
        if (!File.Exists(fmodPath) || !GameContent.HasBanks(gameDir))
        {
            Say.Out.WriteLine($"Not a PoE2 install (need fmod.dll and FMOD\\Desktop or Content.ggpk): {gameDir}");
            return null;
        }

        ExtractorLibraries.Use(Lib, fmodPath);
        if (FMOD_System_Create(out var sys, HeaderVersion(fmodPath)) != 0 || FMOD_System_SetOutput(sys, 2 /*NOSOUND*/) != 0
            || FMOD_System_Init(sys, 8, 0, IntPtr.Zero) != 0)
        {
            Say.Out.WriteLine("FMOD init failed (header version mismatch).");
            return null;
        }

        return sys;
    }

    private static string TempFsb() => Path.Combine(Path.GetTempPath(), "poe2-sounds-" + Environment.ProcessId + ".fsb");

    /// <summary>A bank's FSB5 chunk opened as a sound with sub-sounds (copied to <paramref name="tmp"/>); null when it cannot be.</summary>
    private static IntPtr? OpenBank(IntPtr sys, string gameDir, string name, string tmp)
    {
        if (GameContent.ReadBank(gameDir, name) is not { } bank) { Say.Out.WriteLine($"SKIP bank missing: {name}"); return null; }

        if (Fsb5.Problem(bank) is { } changed) { Say.Out.WriteLine($"SKIP {name}.bank is not the FMOD bank this reader knows: {changed}"); return null; }
        int fsb = bank.AsSpan().IndexOf("FSB5"u8);
        File.WriteAllBytes(tmp, bank.AsSpan(fsb).ToArray());

        if (FMOD_System_CreateSound(sys, tmp, ModeSample, IntPtr.Zero, out var snd) != 0)
        { Say.Out.WriteLine($"SKIP cannot open FSB of {name}"); return null; }
        return snd;
    }

    private static string SampleName(IntPtr sub)
    {
        var buf = new byte[256];
        if (FMOD_Sound_GetName(sub, buf, buf.Length) != 0) return string.Empty;
        int n = Array.IndexOf(buf, (byte)0);
        return Encoding.UTF8.GetString(buf, 0, n < 0 ? buf.Length : n);
    }

    private static IntPtr FindSub(IntPtr snd, int count, string sample)
    {
        for (int i = 0; i < count; i++)
        {
            if (FMOD_Sound_GetSubSound(snd, i, out var sub) != 0 || sub == IntPtr.Zero) continue;
            if (SampleName(sub) == sample) return sub;
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
    // The whoosh chosen for exits plays at RMS 0.31 with full-scale peaks, three times the checkpoint's 0.10.
    private static float[] Level(float[] pcm, double rms)
    {
        if (pcm.Length == 0) return pcm;
        double sum = 0, peak = 0;
        foreach (float v in pcm) { sum += v * v; peak = Math.Max(peak, Math.Abs(v)); }
        double now = Math.Sqrt(sum / pcm.Length);
        if (now <= 0 || peak <= 0) return pcm;
        float gain = (float)Math.Min(rms / now, PeakCap / peak);
        return pcm.Select(v => v * gain).ToArray();
    }

    internal static (float[] pcm, double seconds) TrimAndCap(float[] pcm, int ch, int rate, double maxSeconds)
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

    /// <summary>A loop capped in length, its tail crossfaded (equal power) into its head and dropped.</summary>
    private static (float[] pcm, double seconds) Seamless(float[] pcm, int ch, int rate, double maxSeconds)
    {
        int seam = (int)(LoopSeamSeconds * rate);
        int frames = Math.Min(pcm.Length / ch, (int)(maxSeconds * rate) + seam);
        if (frames <= 2 * seam) return (pcm, (double)pcm.Length / ch / rate);
        int len = frames - seam;
        var res = new float[len * ch];
        Array.Copy(pcm, res, res.Length);
        for (int i = 0; i < seam; i++)
        {
            double angle = Math.PI / 2 * i / seam;
            for (int c = 0; c < ch; c++)
                res[i * ch + c] = (float)(pcm[i * ch + c] * Math.Sin(angle) + pcm[(len + i) * ch + c] * Math.Cos(angle));
        }
        return (res, (double)len / rate);
    }

    /// <summary>A one-shot held still: windowed grains of [from, to) seconds overlap-added round a seamless loop.</summary>
    private static (float[] pcm, double seconds) Freeze(float[] pcm, int ch, int rate, double from, double to)
    {
        int grain = (int)(GrainSeconds * rate), hop = grain / GrainOverlap, len = (int)(FreezeSeconds * rate) / hop * hop;
        int first = (int)(from * rate), last = Math.Min(pcm.Length / ch, (int)(to * rate)) - grain;
        if (last <= first) return Seamless(pcm, ch, rate, LoopMaxSeconds);
        var window = new float[grain];
        for (int i = 0; i < grain; i++) window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / grain));
        var random = new Random(FreezeSeed);
        var res = new float[len * ch];
        for (int start = 0; start < len; start += hop)
        {
            int source = random.Next(first, last);
            for (int i = 0; i < grain; i++)
            {
                int at = (start + i) % len;
                for (int c = 0; c < ch; c++) res[at * ch + c] += pcm[(source + i) * ch + c] * window[i] * 2f / GrainOverlap;
            }
        }
        return (res, (double)len / rate);
    }

    internal static void WriteWav(string path, float[] pcm, int ch, int rate)
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
