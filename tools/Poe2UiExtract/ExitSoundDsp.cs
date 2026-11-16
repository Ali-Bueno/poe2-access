using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// The small, deterministic signal processing the exits' ambient sounds need (docs/research/game-audio.md, "Exits' ambient
/// emitters"): the audition was made with ffmpeg's filters, so each one here follows ffmpeg's definition (RBJ biquads at Q
/// 0.707 unless a width is given, and <c>aecho</c> as a multi-tap echo of the input). Mono float samples, no I/O.
/// </summary>
internal static class ExitSoundDsp
{
    /// <summary>ffmpeg's default Q for its low-pass and high-pass biquads.</summary>
    private const double DefaultQ = 0.707;

    /// <summary>Whether a filter chain (ffmpeg syntax) holds an echo, which the recipe pads and which outlasts its input.</summary>
    public static bool HasEcho(string chain) => Parse(chain).Any(step => step.Name == "aecho");

    /// <summary>The longest delay of the chain's echo, in samples; 0 when it has none.</summary>
    public static int EchoTailSamples(string chain, int rate) =>
        Parse(chain).Where(step => step.Name == "aecho").Select(step => Delays(step, rate).DefaultIfEmpty(0).Max()).DefaultIfEmpty(0).Max();

    /// <summary>
    /// A chain such as <c>lowpass=f=1800,aecho=0.8:0.5:120|240:0.35|0.2</c> run over the samples (the filters start from
    /// silence, as ffmpeg's do); the result is longer than the input by the echo's longest delay.
    /// </summary>
    public static float[] Apply(string chain, float[] input, int rate)
    {
        var x = input;
        foreach (var step in Parse(chain))
        {
            x = step.Name switch
            {
                "lowpass" or "highpass" or "bandpass" => Biquad(step, x, rate),
                "aecho" => Echo(step, x, rate),
                _ => throw new NotSupportedException("filter " + step.Name),
            };
        }

        return x;
    }

    public static double Rms(float[] x, int count = int.MaxValue)
    {
        var n = Math.Min(count, x.Length);
        if (n <= 0)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            sum += (double)x[i] * x[i];
        }

        return Math.Sqrt(sum / n);
    }

    /// <summary>
    /// The sound brought to <paramref name="rms"/> as measured over the first <paramref name="bodySamples"/> samples of
    /// <paramref name="reference"/> (the sound itself when null: an event is measured before its fades), then scaled down if
    /// its peak passes <paramref name="peakLimit"/>.
    /// </summary>
    public static float[] Level(float[] x, double rms, double peakLimit, int bodySamples = int.MaxValue, float[]? reference = null)
    {
        var gain = rms / Math.Max(Rms(reference ?? x, bodySamples), 1e-7);
        var y = new float[x.Length];
        double peak = 0;
        for (var i = 0; i < x.Length; i++)
        {
            y[i] = (float)(x[i] * gain);
            peak = Math.Max(peak, Math.Abs(y[i]));
        }

        if (peak > peakLimit)
        {
            var scale = (float)(peakLimit / peak);
            for (var i = 0; i < y.Length; i++)
            {
                y[i] *= scale;
            }
        }

        return y;
    }

    /// <summary>
    /// A loop of <paramref name="loopSamples"/> whose seam is baked in: the excerpt's first <paramref name="fadeSamples"/>
    /// are crossfaded (equal power) with the samples that follow the loop's end, so the end runs into the start unbroken.
    /// The excerpt must be at least loop + fade long.
    /// </summary>
    public static float[] Seamless(float[] x, int loopSamples, int fadeSamples)
    {
        if (x.Length < loopSamples + fadeSamples)
        {
            throw new ArgumentException($"source too short: {x.Length} < {loopSamples + fadeSamples}");
        }

        var loop = new float[loopSamples];
        Array.Copy(x, loop, loopSamples);
        for (var i = 0; i < fadeSamples; i++)
        {
            var angle = Math.PI / 2 * i / fadeSamples;
            loop[i] = (float)((x[i] * Math.Sin(angle)) + (x[loopSamples + i] * Math.Cos(angle)));
        }

        return loop;
    }

    /// <summary>A copy with a linear fade in over the first and a linear fade out over the last samples.</summary>
    public static float[] Fade(float[] x, int inSamples, int outSamples)
    {
        var y = (float[])x.Clone();
        for (var i = 0; i < Math.Min(inSamples, y.Length); i++)
        {
            y[i] *= (float)i / inSamples;
        }

        for (var i = 0; i < Math.Min(outSamples, y.Length); i++)
        {
            y[y.Length - 1 - i] *= (float)i / outSamples;
        }

        return y;
    }

    private readonly record struct Step(string Name, string[] Args);

    private static IEnumerable<Step> Parse(string chain)
    {
        foreach (var part in chain.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            yield return eq < 0 ? new Step(part, Array.Empty<string>()) : new Step(part[..eq], part[(eq + 1)..].Split(':'));
        }
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>A named option of a biquad step (<c>f=900</c>), or the fallback.</summary>
    private static double Option(Step step, string key, double fallback)
    {
        foreach (var arg in step.Args)
        {
            var eq = arg.IndexOf('=');
            if (eq > 0 && arg[..eq] == key)
            {
                return Number(arg[(eq + 1)..]);
            }
        }

        return fallback;
    }

    private static float[] Biquad(Step step, float[] x, int rate)
    {
        if (step.Args.Any(arg => arg.StartsWith("width_type=", StringComparison.Ordinal) && arg != "width_type=q"))
        {
            throw new NotSupportedException("only a width in Q is supported");
        }

        var w0 = 2 * Math.PI * Option(step, "f", 0) / rate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2 * Option(step, "w", DefaultQ));
        double b0, b1, b2;
        switch (step.Name)
        {
            case "lowpass":
                (b0, b1, b2) = ((1 - cos) / 2, 1 - cos, (1 - cos) / 2);
                break;
            case "highpass":
                (b0, b1, b2) = ((1 + cos) / 2, -(1 + cos), (1 + cos) / 2);
                break;
            default: // bandpass, ffmpeg's default (not constant skirt gain): peak gain alpha
                (b0, b1, b2) = (alpha, 0, -alpha);
                break;
        }

        var a0 = 1 + alpha;
        var (a1, a2) = (-2 * cos / a0, (1 - alpha) / a0);
        (b0, b1, b2) = (b0 / a0, b1 / a0, b2 / a0);
        var y = new float[x.Length];
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (var i = 0; i < x.Length; i++)
        {
            double xn = x[i];
            var yn = (b0 * xn) + (b1 * x1) + (b2 * x2) - (a1 * y1) - (a2 * y2);
            (x2, x1, y2, y1) = (x1, xn, y1, yn);
            y[i] = (float)yn;
        }

        return y;
    }

    private static IEnumerable<int> Delays(Step step, int rate) => step.Args[2].Split('|').Select(ms => (int)(Number(ms) * rate / 1000));

    /// <summary>ffmpeg's aecho: in_gain:out_gain:delays(ms):decays; every tap reads the input, the whole scaled by out_gain.</summary>
    private static float[] Echo(Step step, float[] x, int rate)
    {
        double inGain = Number(step.Args[0]), outGain = Number(step.Args[1]);
        var delays = Delays(step, rate).ToArray();
        var decays = step.Args[3].Split('|').Select(Number).ToArray();
        var y = new float[x.Length + delays.Max()];
        for (var i = 0; i < y.Length; i++)
        {
            double sum = i < x.Length ? x[i] * inGain : 0;
            for (var tap = 0; tap < delays.Length; tap++)
            {
                var from = i - delays[tap];
                if (from >= 0 && from < x.Length)
                {
                    sum += x[from] * decays[tap];
                }
            }

            y[i] = (float)(sum * outGain);
        }

        return y;
    }
}
