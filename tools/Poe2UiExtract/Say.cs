using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

/// <summary>Where the extractor's progress lines go: the console, or the writer the mod's executable gives it.</summary>
public static class Say
{
    private static volatile TextWriter? redirected;
    private static readonly AsyncLocal<Capture?> Captured = new();

    public static TextWriter Out => Captured.Value ?? Current;

    private static TextWriter Current => redirected ?? Console.Out;

    /// <summary>Sends the output to <paramref name="writer"/>; null goes back to the console.</summary>
    public static void Redirect(TextWriter? writer) => redirected = writer;

    /// <summary>
    ///     Keeps the lines the calling command (and the tasks it starts) prints, so a failed batch step can say why in one
    ///     line while the commands beside it run on the same console.
    /// </summary>
    public static Capture StartCapture()
    {
        var capture = new Capture();
        Captured.Value = capture;
        return capture;
    }

    /// <summary>The lines of one command, also passed on to the real output as they are written.</summary>
    public sealed class Capture : TextWriter
    {
        private const int ReasonLines = 4;
        private readonly List<string> lines = new();
        private readonly StringBuilder line = new();

        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>The last lines it printed, where a command that gives up says why.</summary>
        public string Reason()
        {
            lock (this.lines)
            {
                var tail = this.lines.Select(text => text.Trim()).Where(text => text.Length > 0).TakeLast(ReasonLines);
                return string.Join(" | ", tail);
            }
        }

        public override void Write(char value)
        {
            Current.Write(value);
            lock (this.lines)
            {
                if (value == '\n')
                {
                    this.lines.Add(this.line.ToString().TrimEnd('\r'));
                    this.line.Clear();
                }
                else
                {
                    this.line.Append(value);
                }
            }
        }

        public override void Write(string? value)
        {
            Current.Write(value);
            lock (this.lines)
            {
                foreach (var character in value ?? string.Empty)
                {
                    if (character == '\n')
                    {
                        this.lines.Add(this.line.ToString().TrimEnd('\r'));
                        this.line.Clear();
                    }
                    else
                    {
                        this.line.Append(character);
                    }
                }
            }
        }

        public override void WriteLine(string? value)
        {
            Current.WriteLine(value);
            lock (this.lines)
            {
                this.lines.Add(this.line.Append(value).ToString().TrimEnd('\r'));
                this.line.Clear();
            }
        }

        public override void WriteLine() => this.WriteLine(string.Empty);
    }
}
