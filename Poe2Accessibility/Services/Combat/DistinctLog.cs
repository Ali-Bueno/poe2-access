namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Logs each distinct line once under a prefix: what a session's log needs to confirm a rule, not a line per poll.</summary>
    public sealed class DistinctLog
    {
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);
        private readonly PluginLog log;
        private readonly string prefix;

        public DistinctLog(PluginLog log, string prefix) => (this.log, this.prefix) = (log, prefix);

        public void Clear() => this.seen.Clear();

        /// <summary>Logs the line the first time it is seen.</summary>
        public void Note(string line)
        {
            if (this.seen.Add(line))
            {
                this.log.Info($"{this.prefix}: {line}");
            }
        }
    }
}
