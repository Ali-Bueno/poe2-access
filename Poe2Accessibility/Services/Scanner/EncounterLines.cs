namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>Pure: the encounter tracker's labels as lines, a label's buttons joined to what they do.</summary>
    public static class EncounterLines
    {
        /// <summary>Adds a label to the lines; one holding only buttons ("LT + D-pad up" under "Regresar al barco") goes with the line before it.</summary>
        public static void Add(List<string> lines, string text, string tagged)
        {
            var button = ControllerGlyphs.OnlyLeadingButtons(tagged) ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
            var label = button.Length > 0 ? text.Trim().Trim('+').Trim() : text.Trim();
            if (button.Length > 0 && label.Length == 0)
            {
                label = lines.Count > 0 ? lines[^1] : string.Empty;
            }

            if (label.Length == 0)
            {
                return;
            }

            var line = Announcements.WithButton(label, button);
            var known = lines.FindIndex(said => said == label || said == line || said.StartsWith(label + ", ", System.StringComparison.Ordinal));
            if (known < 0)
            {
                lines.Add(line);
            }
            else if (button.Length > 0 && lines[known] == label)
            {
                lines[known] = line;
            }
        }
    }
}
