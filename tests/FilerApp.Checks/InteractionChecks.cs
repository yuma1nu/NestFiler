using FilerApp.Core;

internal static class InteractionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var now = new DateTime(2026, 11, 15, 12, 0, 0);
        check(ModificationDates.Classify(now, now) == ModificationAge.LastDay && ModificationDates.Classify(now.AddHours(-23), now) == ModificationAge.LastDay && ModificationDates.Classify(now.AddDays(-1), now) == ModificationAge.LastWeek, "更新日時: 24時間の境界");
        check(ModificationDates.Classify(now.AddDays(-7), now) == ModificationAge.LastMonth && ModificationDates.Classify(now.AddDays(-30), now) == ModificationAge.LastYear && ModificationDates.Classify(now.AddDays(-365), now) == ModificationAge.Older, "更新日時: 7日・30日・1年の境界");
        check(ModificationDates.Classify(now.AddMinutes(4), now) == ModificationAge.LastDay && ModificationDates.Classify(now.AddMinutes(6), now) == ModificationAge.Future && ModificationDates.Classify(DateTime.MinValue, now) == ModificationAge.Unknown, "更新日時: 時計差・未来・不明を区別");
        var tiles = new MarqueeLayout(10, 3, 2, 2, 100, 80, 96, 76);
        int[] Expand(IEnumerable<IndexSpan> spans) => spans.SelectMany(x => Enumerable.Range(x.Start, x.Length)).Distinct().Order().ToArray();
        check(Expand(tiles.HitTest(0, 0, 200, 80)).SequenceEqual(new[] { 0, 1 }) && tiles.HitTest(99, 3, 101, 70).Count == 0 && tiles.HitTest(103, 243, 195, 300).Count == 0, "矩形選択: タイルの隙間・最終行の空き部分を除外");
        check(Expand(tiles.HitTest(200, 160, 0, 0)).SequenceEqual(Expand(tiles.HitTest(0, 0, 200, 160))) && tiles.HitTest(0, 0, 0, 100).Count == 0, "矩形選択: 逆向きドラッグ・面積ゼロ");
        var random = new Random(871);
        bool geometryCorrect = true;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            int columns = random.Next(1, 8), count = random.Next(1, 500);
            var layout = new MarqueeLayout(count, columns, 2, 2, 100, 80, 96, 76);
            double x1 = random.Next(-100, 800), x2 = random.Next(-100, 800), y1 = random.Next(-100, 10000), y2 = random.Next(-100, 10000);
            var expected = Enumerable.Range(0, count).Where(i => x1 != x2 && y1 != y2 && Math.Min(x1, x2) < 2 + i % columns * 100 + 96 && Math.Max(x1, x2) > 2 + i % columns * 100 && Math.Min(y1, y2) < 2 + i / columns * 80 + 76 && Math.Max(y1, y2) > 2 + i / columns * 80).ToArray();
            if (!Expand(layout.HitTest(x1, y1, x2, y2)).SequenceEqual(expected)) { geometryCorrect = false; break; }
        }
        check(geometryCorrect, "矩形選択: 非表示項目を含む500配置を単純な矩形交差と照合");
        bool combinationsCorrect = true;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            IndexSpan[] Make() => Enumerable.Range(0, 8).Select(_ => new IndexSpan(random.Next(100), random.Next(1, 20))).ToArray();
            var original = Make(); var hit = Make(); var a = Expand(original); var b = Expand(hit);
            foreach (var rule in new[] { SelectionRule.Replace, SelectionRule.Add, SelectionRule.Toggle, SelectionRule.Subtract })
            {
                var expected = rule switch { SelectionRule.Replace => b, SelectionRule.Add => a.Union(b).Order().ToArray(), SelectionRule.Toggle => a.Except(b).Union(b.Except(a)).Order().ToArray(), _ => a.Except(b).ToArray() };
                if (!Expand(SelectionSpans.Combine(original, hit, rule)).SequenceEqual(expected)) combinationsCorrect = false;
            }
        }
        check(combinationsCorrect, "矩形選択: 通常・Shift追加・Ctrl反転・差分を800パターン照合");
        var large = new MarqueeLayout(1_000_000, 1, 0, 0, 300, 26, 300, 26).HitTest(0, 0, 300, 26_000_000);
        check(large.SequenceEqual(new[] { new IndexSpan(0, 1_000_000) }), "矩形選択: 大量の詳細行を1つの選択範囲で表現");
    }
}
