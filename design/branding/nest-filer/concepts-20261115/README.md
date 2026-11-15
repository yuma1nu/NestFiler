# Nest Filer: icon concepts

2026-11-15。名称とアイコンの比較用。アプリの表示名・実行ファイル・製品アイコンにはまだ適用していない。

生成方法: built-in `image_gen.imagegen`。CLI/APIフォールバックは使用していない。各案を別の新規画像として、`transparent_background: true`で生成。出力をそのままコピーし、元の生成ファイルも保持している。最終的なICOや16～48px用の調整は未実施。

| 案 | ファイル | 意図 |
|---|---|---|
| A | [A-nested-folders.png](A-nested-folders.png) | 入れ子のお気に入りと、まとまった作業環境 |
| B | [B-nest.png](B-nest.png) | Nest＝巣。フォルダを包む独自の形 |
| C | [C-n-monogram.png](C-n-monogram.png) | Nとフォルダを組み合わせた記号 |
| D | [D-two-panes.png](D-two-panes.png) | ひとつのフォルダ内に左右2ペイン |

名称案: 人が読む表示名は`Nest Filer`、内部名や実行ファイル名は`NestFiler`／`NestFiler.exe`と使い分けられる。スペースを含む表示名自体に問題はない。表記を一本化する場合は`NestFiler`も候補。今回は名称変更の実装は行っていない。

## Generation prompts

以下の共通接頭辞に、各案の本文を連結して使用。

```text
Use case: logo-brand. Asset type: app icon concept exploration, one square image.
```

### A

```text
A distinctive desktop app icon for a Windows 11 file manager named Nest Filer. Concept A: two or three neatly nested folder silhouettes, a warm amber outer folder cradling a smaller pale cream folder and a single teal inset, suggesting organized favorite folders and a personal workspace. Strong recognizable compact silhouette, very simple at 24px, no thin detail, minimal flat vector-like geometry with subtle Fluent-style dimensional layering. NOT a generic yellow Windows Explorer clone. No rounded-square container around the icon, no typography, no letter, no labels, no watermark. Centered isolated icon, full generous padding, genuinely transparent background.
```

### B

```text
A distinctive desktop app icon for a Windows 11 file manager named Nest Filer. Concept B: an abstract nest made from only three broad interlocking curved bands, cradling a single small folder tab in its center. Blend a calm teal nest with warm amber folder center. The nest must be geometric and restrained, not twigs, not a bird, not an egg. Strong recognizable compact silhouette, very simple at 24px, no thin detail, minimal flat vector-like geometry with subtle Fluent-style dimensional layering. No rounded-square container around the icon, no typography, no labels, no watermark. Centered isolated icon, full generous padding, genuinely transparent background.
```

### C

```text
A distinctive desktop app icon for a Windows 11 file manager named Nest Filer. Concept C: a bold geometric capital N monogram constructed out of two folded file folder tabs or folded document panels. Integrate one negative-space folder notch so it reads as an N and a workspace container. Deep teal and warm amber, balanced professional engineering productivity app, warm not corporate. Strong recognizable compact silhouette, very simple at 24px, no thin detail, minimal flat vector-like geometry with subtle Fluent-style dimensional layering. No rounded-square container around the icon. Only the abstract N mark, no wordmark, no other letters, no labels, no watermark. Centered isolated icon, full generous padding, genuinely transparent background.
```

### D

```text
A distinctive desktop app icon for a Windows 11 file manager named Nest Filer. Concept D: one compact open folder shape with a clean centered vertical division creating two side-by-side pane shapes. Left half warm amber, right half muted teal, both tucked into one shared folder base, communicating two independently usable file panes within one organized workspace. Strong recognizable compact silhouette, very simple at 24px, no thin detail, minimal flat vector-like geometry with subtle Fluent-style dimensional layering. Distinct from Windows Explorer. No tiny file list lines, no magnifier, no arrows, no rounded-square container around the icon, no typography, no letter, no labels, no watermark. Centered isolated icon, full generous padding, genuinely transparent background.
```
