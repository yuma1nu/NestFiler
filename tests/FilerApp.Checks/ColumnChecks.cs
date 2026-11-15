using FilerApp.Core;

internal static class ColumnChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        var normal = new FileColumns(false); var search = new FileColumns(true);
        check(normal.Columns.Count == 4 && search.Columns.Select(x => x.Key).Contains("Match") && search.Columns.Select(x => x.Key).Contains("Path") && search.Columns.Select(x => x.Key).Contains("Line"), "列: 通常と検索で異なる列構成");
        normal.Resize("Name", 280); normal.Move("Size", "Name", true);
        check(normal.Columns[0].Key == "Size" && normal.Columns.Single(x => x.Key == "Name").Width == 280 && !normal.FillName, "列: 幅変更と前への移動を保持");
        normal.Move("Size", "Kind", false);
        check(normal.Columns[^1].Key == "Size" && search.Columns[0].Key == "Name", "列: 後ろへの移動と通常・検索の独立");
        var saved = normal.Capture(); normal.Resize("Name", 600);
        var restored = new FileColumns(false, saved);
        check(restored.Columns.SequenceEqual(saved.Columns) && restored.Columns.Single(x => x.Key == "Name").Width == 280, "列: 保存内容を共有参照で変更せず復元");
        var malformed = new FileColumns(false, new() { Columns = [new("unknown", 300), new("Name", 1), new("Name", 600), new("Modified", double.NaN)] });
        check(malformed.Columns.Count == 4 && malformed.Columns.Select(x => x.Key).Distinct().Count() == 4 && malformed.Columns.All(x => double.IsFinite(x.Width) && x.Width >= 40) && malformed.Columns[0].Width == 80, "列: 不明・重複・過小幅・非数値を補正");
        var fit = new FileColumns(false); fit.Fit(1000);
        var filled = fit.TotalWidth; fit.Resize("Name", 200); fit.Fit(1400);
        check(filled == 1000 && fit.Columns.Single(x => x.Key == "Name").Width == 200, "列: 初期幅は余白に追従し手動調整後は維持");
        restored.Reset(); check(restored.Columns[0].Key == "Name" && restored.FillName, "列: 順序と幅を初期化");
        var state = new SessionState { LeftColumns = saved, LeftSearchColumns = search.Capture(), RightColumns = new FileColumns(false).Capture() };
        var store = new StateStore(Path.Combine(root, "column-state.db")); await store.SaveAsync(state); var loaded = await store.LoadAsync();
        check(loaded.LeftColumns!.Columns.SequenceEqual(saved.Columns) && loaded.LeftSearchColumns!.Columns.Count == 7 && loaded.RightColumns!.Columns[0].Key == "Name", "列: A/Bと通常/検索の順序・幅をSQLite保存");
    }
}
