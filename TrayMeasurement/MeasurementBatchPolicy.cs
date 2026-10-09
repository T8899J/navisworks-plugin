using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JiePinPai.Navisworks.TrayMeasurement
{
    internal enum MeasurementSourceKind { SearchResults, ManualSelection }

    internal sealed class MeasurementSourceStamp
    {
        public MeasurementSourceKind Kind { get; }
        public long ModelRevision { get; }
        public long SearchRevision { get; }

        public MeasurementSourceStamp(MeasurementSourceKind kind, long modelRevision, long searchRevision)
        {
            Kind = kind;
            ModelRevision = modelRevision;
            SearchRevision = searchRevision;
        }

        public bool IsCurrent(long modelRevision, long searchRevision, bool documentAvailable, bool searchAvailable)
        {
            return documentAvailable && ModelRevision == modelRevision
                && (Kind == MeasurementSourceKind.ManualSelection || searchAvailable && SearchRevision == searchRevision);
        }
    }

    internal enum MeasurementState { Pending, Measured, Review, Cancelled }

    internal sealed class MeasurementValue
    {
        public MeasurementState State { get; private set; } = MeasurementState.Pending;
        public double? Metres { get; private set; }
        public string Note { get; private set; } = "等待测量";

        public void Complete(bool straight, double metres, string reason)
        {
            bool valid = straight && metres > 0 && !double.IsNaN(metres) && !double.IsInfinity(metres);
            State = valid ? MeasurementState.Measured : MeasurementState.Review;
            Metres = valid ? (double?)metres : null;
            Note = valid ? "直桥架几何长度" : (string.IsNullOrWhiteSpace(reason) ? "无法可靠测量" : reason);
        }

        public void Cancel()
        {
            if (State != MeasurementState.Pending) return;
            State = MeasurementState.Cancelled;
            Note = "未测量";
        }

        public string StatusText => State == MeasurementState.Measured ? "已测量"
            : State == MeasurementState.Review ? "待复核"
            : State == MeasurementState.Cancelled ? "未测量" : "等待测量";
    }

    internal static class MeasurementBatchPolicy
    {
        public static List<T> SnapshotTargets<T>(IEnumerable<T> items) where T : class
        {
            return (items ?? Enumerable.Empty<T>()).Where(item => item != null).Distinct().ToList();
        }

        // A selected assembly and its selected descendants must never both contribute length.
        // Mark the entire overlapping group for review instead of choosing a level silently.
        public static HashSet<T> FindOverlaps<T>(IEnumerable<T> items, Func<T, T> parent) where T : class
        {
            var selected = new HashSet<T>(items.Where(item => item != null));
            var overlaps = new HashSet<T>();
            foreach (T item in selected)
            {
                var visited = new HashSet<T> { item };
                for (T ancestor = parent(item); ancestor != null && visited.Add(ancestor); ancestor = parent(ancestor))
                {
                    if (!selected.Contains(ancestor)) continue;
                    overlaps.Add(item);
                    overlaps.Add(ancestor);
                }
            }
            return overlaps;
        }

        public static double Total(IEnumerable<MeasurementValue> values)
        {
            return values.Where(v => v.State == MeasurementState.Measured && v.Metres.HasValue)
                .Sum(v => v.Metres.Value);
        }

        public static string Csv(string value)
        {
            value = value ?? string.Empty;
            string leading = value.TrimStart();
            // Model names and condition values are untrusted spreadsheet text.
            if (leading.Length > 0 && "=+-@".IndexOf(leading[0]) >= 0
                || value.Length > 0 && "\t\r\n".IndexOf(value[0]) >= 0)
                value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string FormatMetres(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
