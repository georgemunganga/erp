using System.Diagnostics.Metrics;

// Private probe diagnostics only. Application readiness publishes fixed phase names;
// this listener records bounded aggregates without SQL, URLs, identities or payloads.
internal sealed class PrincipalApprovalProbeTiming : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, Phase> phases = new(StringComparer.Ordinal);
    private readonly MeterListener listener = new();
    private int discarded;

    private sealed class Phase
    {
        internal int Count;
        internal double TotalMilliseconds;
        internal double MaximumMilliseconds;
    }

    internal PrincipalApprovalProbeTiming()
    {
        listener.InstrumentPublished = (instrument, source) =>
        {
            if (instrument.Meter.Name == "GreenWebb.Procurement.Readiness"
                && instrument.Name == "procurement.readiness.phase.duration")
                source.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, duration, tags, _) =>
        {
            string? phase = null;
            foreach (var tag in tags)
                if (tag.Key == "phase") { phase = tag.Value as string; break; }
            if (phase is null || phase.Length is < 1 or > 80
                || !phase.All(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '-' or '_')) return;
            if (!double.IsFinite(duration) || duration < 0) return;
            lock (gate)
            {
                if (!phases.TryGetValue(phase, out var entry))
                {
                    if (phases.Count == 64) { discarded++; return; }
                    entry = new Phase();
                    phases.Add(phase, entry);
                }
                entry.Count++;
                entry.TotalMilliseconds += duration;
                entry.MaximumMilliseconds = Math.Max(entry.MaximumMilliseconds, duration);
            }
        });
        listener.Start();
    }

    internal object Snapshot()
    {
        lock (gate)
            return new
            {
                meter = "GreenWebb.Procurement.Readiness",
                metric = "procurement.readiness.phase.duration",
                phases = phases.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new
                {
                    name = x.Key, count = x.Value.Count,
                    totalMilliseconds = Math.Round(x.Value.TotalMilliseconds, 3),
                    maximumMilliseconds = Math.Round(x.Value.MaximumMilliseconds, 3),
                }).ToArray(),
                discarded,
            };
    }

    public void Dispose() => listener.Dispose();
}
