using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace KH.PluginKit
{
    /// <summary>
    /// Writes to the platform's tracing service with the correlation id at the start of every line. The
    /// correlation id is shared by every plugin and depth that one operation triggers, so the lines of one
    /// operation can be picked out of the plugin trace log.
    /// </summary>
    public sealed class PluginTrace
    {
        private readonly ITracingService _sink;
        private readonly string _prefix;

        public Guid Correlation { get; }

        public PluginTrace(ITracingService sink, Guid correlation)
        {
            _sink = sink;
            Correlation = correlation;
            _prefix = "[" + correlation.ToString("D") + "]";
        }

        public void Info(string format, params object[] args) => Line("INFO", format, args);

        public void Warn(string format, params object[] args) => Line("WARN", format, args);

        private void Line(string level, string format, object[] args)
        {
            var message = args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args);
            _sink.Trace("{0} {1} {2}", _prefix, level, message);
        }
    }
}
