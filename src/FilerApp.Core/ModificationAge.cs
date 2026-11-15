namespace FilerApp.Core;

public enum ModificationAge { LastDay, LastWeek, LastMonth, LastYear, Older, Future, Unknown }

public static class ModificationDates
{
    public static ModificationAge Classify(DateTime modified, DateTime now)
    {
        if (modified == DateTime.MinValue || modified == DateTime.MaxValue) return ModificationAge.Unknown;
        var elapsed = now - modified;
        if (elapsed < TimeSpan.FromMinutes(-5)) return ModificationAge.Future;
        return elapsed.TotalDays < 1 ? ModificationAge.LastDay : elapsed.TotalDays < 7 ? ModificationAge.LastWeek :
            elapsed.TotalDays < 30 ? ModificationAge.LastMonth : elapsed.TotalDays < 365 ? ModificationAge.LastYear : ModificationAge.Older;
    }
}
