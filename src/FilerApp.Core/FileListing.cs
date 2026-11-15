using System.IO.Enumeration;

namespace FilerApp.Core;

// Numeric runs are compared without parsing integers, including very long IDs.
public sealed class NaturalNameComparer : IComparer<string>
{
    public static NaturalNameComparer Instance { get; } = new();
    public int Compare(string? a, string? b)
    {
        if (a == null || b == null) return a == b ? 0 : a == null ? -1 : 1;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                int numberA = startA, numberB = startB;
                while (numberA < i && a[numberA] == '0') numberA++;
                while (numberB < j && b[numberB] == '0') numberB++;
                int result = (i - numberA).CompareTo(j - numberB);
                if (result == 0) result = a.AsSpan(numberA, i - numberA).SequenceCompareTo(b.AsSpan(numberB, j - numberB));
                if (result != 0) return result;
                result = (i - startA).CompareTo(j - startB);
                if (result != 0) return result;
            }
            else
            {
                int result = char.ToUpperInvariant(a[i++]).CompareTo(char.ToUpperInvariant(b[j++]));
                if (result != 0) return result;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}

public static class FileNames
{
    public static bool Matches(string name, string query, bool caseSensitive = false) =>
        query.IndexOfAny(['*', '?']) >= 0
            ? FileSystemName.MatchesSimpleExpression(query, name, !caseSensitive)
            : name.Contains(query, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}

public sealed class ListingPosition(string[] selected, string? anchor, double anchorOffset, double verticalOffset, double horizontalOffset)
{
    public string[] Selected { get; } = selected;
    public string? Anchor { get; } = anchor;
    public double AnchorOffset { get; } = anchorOffset;
    public double VerticalOffset { get; } = verticalOffset;
    public double HorizontalOffset { get; } = horizontalOffset;
}
