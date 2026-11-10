namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    ///     The requirements a character does not meet, from a tooltip line's marked-up text: the game colours only the unmet
    ///     value ("Requiere: &lt;valuedefault&gt;{Nivel 19, &lt;augmented&gt;{10} Fue, &lt;unmet&gt;{21} Int}"; tooltip dump 2026-11-10). Pure.
    /// </summary>
    public static class UnmetRequirements
    {
        private const string UnmetTag = "unmet";

        /// <summary>Each unmet requirement as the line words it ("21 Int", "Nivel 30"), in order; empty when all are met.</summary>
        public static List<string> Of(string tagged)
        {
            var found = new List<string>();
            if (!tagged.Contains($"<{UnmetTag}>", StringComparison.Ordinal))
            {
                return found;
            }

            var part = new StringBuilder();
            var partUnmet = false;
            var open = new Stack<bool>(); // one entry per "{": whether its tag was the unmet one
            var unmetDepth = 0;
            for (var i = 0; i < tagged.Length; i++)
            {
                var c = tagged[i];
                if (c == '<' && tagged.IndexOf('>', i) is > 0 and var close)
                {
                    var doubled = i + 1 < tagged.Length && tagged[i + 1] == '<';
                    var end = doubled ? tagged.IndexOf(">>", i, StringComparison.Ordinal) + 1 : close;
                    if (end <= 0)
                    {
                        break;
                    }

                    if (!doubled && end + 1 < tagged.Length && tagged[end + 1] == '{')
                    {
                        var unmet = tagged.AsSpan(i + 1, end - i - 1).SequenceEqual(UnmetTag);
                        open.Push(unmet);
                        unmetDepth += unmet ? 1 : 0;
                        end++;
                    }

                    i = end;
                }
                else if (c == '}' && open.Count > 0)
                {
                    unmetDepth -= open.Pop() ? 1 : 0;
                }
                else if (c == '[' && tagged.IndexOf(']', i) is > 0 and var linkEnd)
                {
                    // A keyword link "[Key|shown]": only what is shown.
                    var bar = tagged.IndexOf('|', i, linkEnd - i);
                    part.Append(tagged, (bar > 0 ? bar : i) + 1, linkEnd - (bar > 0 ? bar : i) - 1);
                    partUnmet |= unmetDepth > 0;
                    i = linkEnd;
                }
                else if (c is ',' or '\n')
                {
                    Close();
                }
                else
                {
                    part.Append(c);
                    partUnmet |= unmetDepth > 0 && !char.IsWhiteSpace(c);
                }
            }

            Close();
            return found;

            void Close()
            {
                var text = part.ToString();
                // The line's own label ("Requiere: Nivel 30") is not part of the first requirement.
                text = (text.LastIndexOf(": ", StringComparison.Ordinal) is >= 0 and var label ? text[(label + 2)..] : text).Trim();
                if (partUnmet && text.Length > 0 && !found.Contains(text))
                {
                    found.Add(text);
                }

                part.Clear();
                partUnmet = false;
            }
        }
    }
}
