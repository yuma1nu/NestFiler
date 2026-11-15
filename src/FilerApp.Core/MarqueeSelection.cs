namespace FilerApp.Core;

public readonly record struct IndexSpan(int Start, int Length) { public int End => Start + Length; }
public enum SelectionRule { Replace, Add, Toggle, Subtract }

// Uniform row/tile geometry lets offscreen items participate without realizing
// every container. End-exclusive spans also keep large selections inexpensive.
public sealed record MarqueeLayout(int Count, int Columns, double OriginX, double OriginY,
    double StepX, double StepY, double ItemWidth, double ItemHeight)
{
    public List<IndexSpan> HitTest(double x1, double y1, double x2, double y2)
    {
        var result = new List<IndexSpan>();
        if (Count <= 0 || Columns <= 0 || StepX <= 0 || StepY <= 0 || ItemWidth <= 0 || ItemHeight <= 0) return result;
        var left = Math.Min(x1, x2); var right = Math.Max(x1, x2);
        var top = Math.Min(y1, y2); var bottom = Math.Max(y1, y2);
        if (!double.IsFinite(left + right + top + bottom) || left == right || top == bottom) return result;
        var firstColumn = (int)Math.Clamp(Math.Floor((left - OriginX - ItemWidth) / StepX) + 1, 0, Columns);
        var lastColumn = (int)Math.Clamp(Math.Ceiling((right - OriginX) / StepX) - 1, -1, Columns - 1);
        var rows = (Count - 1) / Columns + 1;
        var firstRow = (int)Math.Clamp(Math.Floor((top - OriginY - ItemHeight) / StepY) + 1, 0, rows);
        var lastRow = (int)Math.Clamp(Math.Ceiling((bottom - OriginY) / StepY) - 1, -1, rows - 1);
        if (firstColumn > lastColumn || firstRow > lastRow) return result;
        if (firstColumn == 0 && lastColumn == Columns - 1)
        {
            var start = firstRow * Columns;
            result.Add(new(start, (int)Math.Min(Count, (long)(lastRow + 1) * Columns) - start));
            return result;
        }
        for (int row = firstRow; row <= lastRow; row++)
        {
            int start = row * Columns + firstColumn, end = Math.Min(Count, row * Columns + lastColumn + 1);
            if (end <= start) continue;
            if (result.Count > 0 && result[^1].End == start) result[^1] = new(result[^1].Start, end - result[^1].Start);
            else result.Add(new(start, end - start));
        }
        return result;
    }
}

public static class SelectionSpans
{
    public static List<IndexSpan> Combine(IEnumerable<IndexSpan> original, IEnumerable<IndexSpan> hit, SelectionRule rule)
    {
        var events = new SortedDictionary<int, (int A, int B)>();
        void Add(IEnumerable<IndexSpan> spans, bool second)
        {
            foreach (var span in spans.Where(x => x.Start >= 0 && x.Length > 0))
            {
                var start = events.GetValueOrDefault(span.Start); var end = events.GetValueOrDefault(span.End);
                events[span.Start] = second ? (start.A, start.B + 1) : (start.A + 1, start.B);
                events[span.End] = second ? (end.A, end.B - 1) : (end.A - 1, end.B);
            }
        }
        Add(original, false); Add(hit, true);
        var result = new List<IndexSpan>(); int a = 0, b = 0, previous = 0;
        foreach (var (position, change) in events)
        {
            bool selected = rule switch { SelectionRule.Replace => b > 0, SelectionRule.Add => a > 0 || b > 0, SelectionRule.Toggle => (a > 0) != (b > 0), _ => a > 0 && b == 0 };
            if (selected && position > previous)
            {
                if (result.Count > 0 && result[^1].End == previous) result[^1] = new(result[^1].Start, position - result[^1].Start);
                else result.Add(new(previous, position - previous));
            }
            a += change.A; b += change.B; previous = position;
        }
        return result;
    }
}
