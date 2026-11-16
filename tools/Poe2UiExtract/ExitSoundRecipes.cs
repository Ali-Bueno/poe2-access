using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// The recipes of the exits' ambient sounds (<c>Poe2Accessibility/catalog/exit-sounds.json</c>: where each piece is cut from
/// the player's own banks and how it is processed; no game audio) and the rendering of one set into mono float files.
/// A bed is a loop with its seam baked in, an event a short one-shot; the output names are
/// <c>bed_&lt;Set&gt;&lt;n&gt;</c> and <c>event_&lt;Set&gt;&lt;n&gt;</c> inside the material's folder.
/// </summary>
internal sealed class ExitSoundRecipes
{
    public const string FileName = "exit-sounds.json";

    // A water drop: a sine gliding down for 0.18 s behind a 2 ms click, echoed twice (echo gains 0.45 and 0.2).
    private const int ClickSamples = 88;
    private const double ClickLevel = 0.25;
    private const int DropSeedBase = 11;
    private const double DropTailSeconds = 0.35;
    private const double DropLeadSeconds = 0.2;
    private const double DropToneSeconds = 0.18;
    private const double DropAttackSeconds = 0.003;
    private const double FirstEchoGain = 0.45;
    private const double SecondEchoGain = 0.2;

    public sealed record Piece(string Bank, string Sample, double Start, double Length, string Filter);

    public sealed record Drop(double StartHz, double EndHz, double GlideS, double DecayS, double Echo1S, double Echo2S);

    public sealed record Set(string Material, string Name, IReadOnlyList<Piece> Beds, IReadOnlyList<Piece> Events, IReadOnlyList<Drop> Drops);

    public int Rate { get; private init; }

    public double LevelRms { get; private init; }

    public double PeakLimit { get; private init; }

    public double LoopSeconds { get; private init; }

    public double CrossfadeSeconds { get; private init; }

    public double FadeInSeconds { get; private init; }

    public double FadeOutSeconds { get; private init; }

    public double EchoPadSeconds { get; private init; }

    public double DropLevelSeconds { get; private init; }

    public IReadOnlyList<Set> Sets { get; private init; } = Array.Empty<Set>();

    public static ExitSoundRecipes Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        double Number(string key) => root.GetProperty(key).GetDouble();
        var sets = root.GetProperty("sets").EnumerateArray().Select(set => new Set(
            set.GetProperty("material").GetString()!,
            set.GetProperty("set").GetString()!,
            Pieces(set, "beds"),
            Pieces(set, "events"),
            set.TryGetProperty("drops", out var drops)
                ? drops.EnumerateArray().Select(drop => new Drop(drop.GetProperty("startHz").GetDouble(), drop.GetProperty("endHz").GetDouble(),
                    drop.GetProperty("glideS").GetDouble(), drop.GetProperty("decayS").GetDouble(),
                    drop.GetProperty("echo1S").GetDouble(), drop.GetProperty("echo2S").GetDouble())).ToList()
                : new List<Drop>())).ToList();
        return new ExitSoundRecipes
        {
            Rate = (int)Number("sampleRate"),
            LevelRms = Number("levelRms"),
            PeakLimit = Number("peakLimit"),
            LoopSeconds = Number("loopSeconds"),
            CrossfadeSeconds = Number("crossfadeSeconds"),
            FadeInSeconds = Number("eventFadeInSeconds"),
            FadeOutSeconds = Number("eventFadeOutSeconds"),
            EchoPadSeconds = Number("echoPadSeconds"),
            DropLevelSeconds = Number("dropLevelSeconds"),
            Sets = sets,
        };
    }

    /// <summary>The (bank, sample) pairs every set reads.</summary>
    public IEnumerable<(string Bank, string Sample)> Sources() =>
        this.Sets.SelectMany(set => set.Beds.Concat(set.Events)).Select(piece => (piece.Bank, piece.Sample)).Distinct();

    /// <summary>
    /// A set's files by name. <paramref name="source"/> gives a sample as the extractor decodes it (mono, leading silence
    /// trimmed, capped at 4 s) or null when the bank lacks it: that file is left out and listed in <paramref name="missing"/>.
    /// </summary>
    public Dictionary<string, float[]> Render(Set set, Func<string, string, float[]?> source, List<string> missing)
    {
        var files = new Dictionary<string, float[]>();
        for (var i = 0; i < set.Beds.Count; i++)
        {
            var bed = set.Beds[i];
            var name = $"bed_{set.Name}{i + 1}";
            if (source(bed.Bank, bed.Sample) is { } sample)
            {
                files[name] = this.Bed(bed, sample);
            }
            else
            {
                missing.Add($"{set.Material}/{name} ({bed.Bank}/{bed.Sample})");
            }
        }

        for (var i = 0; i < set.Events.Count; i++)
        {
            var piece = set.Events[i];
            var name = $"event_{set.Name}{i + 1}";
            if (source(piece.Bank, piece.Sample) is { } sample)
            {
                files[name] = this.Event(piece, sample);
            }
            else
            {
                missing.Add($"{set.Material}/{name} ({piece.Bank}/{piece.Sample})");
            }
        }

        for (var i = 0; i < set.Drops.Count; i++)
        {
            files[$"event_{set.Name}{i + 1}"] = this.DropFile(set.Drops[i], i);
        }

        return files;
    }

    /// <summary>A bed: an excerpt a loop and a crossfade long, filtered, its seam baked in, levelled.</summary>
    public float[] Bed(Piece piece, float[] sample)
    {
        var loop = (int)(this.LoopSeconds * this.Rate);
        var fade = (int)(this.CrossfadeSeconds * this.Rate);
        var excerpt = Excerpt(sample, piece.Start, (double)(loop + fade) / this.Rate, this.Rate);
        var filtered = piece.Filter.Length == 0 ? excerpt : ExitSoundDsp.Apply(piece.Filter, excerpt, this.Rate);
        return ExitSoundDsp.Level(ExitSoundDsp.Seamless(filtered, loop, fade), this.LevelRms, this.PeakLimit);
    }

    /// <summary>
    /// An event: a cut of <paramref name="piece"/>.Length seconds, with a silent pad after it when the filter chain holds an echo
    /// (so the echoes ring out), filtered, faded, and levelled by the first <c>Length</c> seconds' loudness.
    /// </summary>
    public float[] Event(Piece piece, float[] sample)
    {
        var length = Math.Min(piece.Length, (double)sample.Length / this.Rate);
        var cut = Excerpt(sample, piece.Start, length, this.Rate);
        var x = cut;
        if (piece.Filter.Length > 0)
        {
            if (ExitSoundDsp.HasEcho(piece.Filter))
            {
                x = new float[cut.Length + (int)(this.EchoPadSeconds * this.Rate)];
                Array.Copy(cut, x, cut.Length);
            }

            x = ExitSoundDsp.Apply(piece.Filter, x, this.Rate);
        }

        var faded = ExitSoundDsp.Fade(x, (int)(this.FadeInSeconds * this.Rate), (int)(this.FadeOutSeconds * this.Rate));
        return ExitSoundDsp.Level(faded, this.LevelRms, this.PeakLimit, (int)(length * this.Rate), x);
    }

    /// <summary>
    /// A water drop, synthesised (the game has no dripping sound): a sine gliding down and dying fast, a 2 ms click, echoed at
    /// two delays. The click's noise is seeded by the drop's index, so the file is the same on every machine.
    /// </summary>
    public float[] DropFile(Drop drop, int index)
    {
        var rate = this.Rate;
        var random = new Random(DropSeedBase + index);
        var tone = new double[(int)(DropToneSeconds * rate)];
        var phase = 0.0;
        for (var k = 0; k < tone.Length; k++)
        {
            var t = (double)k / rate;
            var hz = drop.EndHz + ((drop.StartHz - drop.EndHz) * Math.Exp(-t / drop.GlideS));
            phase += 2 * Math.PI * hz / rate;
            tone[k] = Math.Sin(phase) * Math.Exp(-t / drop.DecayS) * Math.Min(1.0, k / (DropAttackSeconds * rate));
        }

        for (var k = 0; k < ClickSamples; k++)
        {
            tone[k] += ((random.NextDouble() * 2) - 1) * ClickLevel * (1 - ((double)k / ClickSamples));
        }

        var y = new float[(int)((DropLeadSeconds + drop.Echo2S + DropTailSeconds) * rate)];
        foreach (var (delay, gain) in new[] { (0.0, 1.0), (drop.Echo1S, FirstEchoGain), (drop.Echo2S, SecondEchoGain) })
        {
            var offset = (int)(delay * rate);
            for (var k = 0; k < tone.Length; k++)
            {
                y[offset + k] += (float)(tone[k] * gain);
            }
        }

        var faded = ExitSoundDsp.Fade(y, (int)(this.FadeInSeconds * rate), (int)(this.FadeOutSeconds * rate));
        return ExitSoundDsp.Level(faded, this.LevelRms, this.PeakLimit, (int)(this.DropLevelSeconds * rate), y);
    }

    private static float[] Excerpt(float[] sample, double start, double length, int rate)
    {
        var from = Math.Min(sample.Length, (int)Math.Round(start * rate));
        var count = Math.Min(sample.Length - from, (int)Math.Round(length * rate));
        var cut = new float[count];
        Array.Copy(sample, from, cut, 0, count);
        return cut;
    }

    private static List<Piece> Pieces(JsonElement set, string key) => set.GetProperty(key).EnumerateArray().Select(piece => new Piece(
        piece.GetProperty("bank").GetString()!,
        piece.GetProperty("sample").GetString()!,
        piece.GetProperty("start").GetDouble(),
        piece.TryGetProperty("length", out var length) ? length.GetDouble() : 0,
        piece.GetProperty("filter").GetString()!)).ToList();
}
