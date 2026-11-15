# 行クリック・マウス履歴・複数選択の改善

2026-11-15。Debug確認版のみ更新。Release版・配布ZIPは変更していない。

その後の列位置・ドラッグ対象・500画像一覧の描画負荷対策と最新の検証は [DRAG-RESIZE-RESPONSIVENESS.md](DRAG-RESIZE-RESPONSIVENESS.md) に記録。この文書の選択APIの数値と、後続のUI応答間隔の数値は測定対象が異なる。

## 操作

- マウスの戻る（XButton1）／進む（XButton2）を、そのペインの通常タブの移動履歴へ接続。サイドボタンを押したペインを選択する。サイドバーなどではアクティブペインが対象。モーダルプレビュー／ダイアログの背後は移動しない。
- 通常一覧・検索一覧とも、ファイル名の文字だけでなく**行全体**をクリック・ダブルクリック・Ctrl/Shift選択・ファイルドラッグの対象にした。列の合計幅がペインより狭い場合も、右側の余白を含む。
- 行高は26→28 DIP。パディングと行間は0に揃え、上下端にもヒット判定を持たせた。値は `App.xaml` の `FileRowHeight` に集約。
- 矩形の範囲選択は、一覧左端の6 DIPの余白、項目のない背景、サムネイルの項目間から開始できる。行の中からのドラッグはファイル転送。クリック範囲と矩形選択の開始範囲が競合しないようにした。

## 軽量化

- 選択変更の通知をUIキューでまとめ、件数・容量・プレビュー通知・保存予約は入力処理後に1回実行。50回の選択通知でも集計を1回にする。
- 選択した項目は `SelectedRanges` とモデルの添字から取得する。`SelectedItems` のWinRTコレクション全体を選択のたびに列挙し直す処理を除去。選択件数や容量の集計でファイルシステムへアクセスしない。
- 選択の復元では、500件を1件ずつ追加する方式をやめ、連続したインデックス範囲の差分を `SelectRange`／`DeselectRange` に渡す。変更がない選択は触らない。
- 切り取り中の外観は切り取り内容の変更／行の再利用時に更新。単に選択しただけでは行のOpacityを再設定しない。
- 行のアクセシビリティ名をファイル名に設定。自動生成されたFileEntry全体の文字列化・全プロパティの読み出しを避け、パスは説明へ分離した。

## 計測と確認

専用500ファイル・同一PC・Debug/x64。選択APIの同期実行部分を6回計測した値。描画完了や入力装置からの遅延、実ユーザーフォルダのShiftクリック全体を測る数値ではない。

| 計測 | 変更前 | 変更後 |
|---|---:|---:|
| 500件SelectRangeの同期処理（中央値） | 4.42ms | 4.07ms |
| 同じ500件の選択復元 | 75.31ms | 3.87ms |
| クリアされた500件を再選択して復元 | 未分離 | 4.62ms |
| 実体化した行数 | 31/500 | 31/500 |

大きく短縮したのは選択復元とアプリ側の重複処理。通常の範囲選択APIそのものは前後とも約4msであり、Shiftクリックが一律何倍速くなったとはしていない。画像デコード中・共有フォルダ・DPI・GPUを含めた描画性能は別途確認が必要。

- `scripts/CheckInput.ps1`: **追加7件**。500件の選択復元、50通知の集約、行の上下端と右側余白の実ヒットテスト、アクセシビリティ名、仮想化維持、A/Bそれぞれのマウス履歴コマンドルートを確認。
- `scripts/CheckPaneFocus.ps1`: 既存のWinUI統合 **44件**。列・検索・プレビュー・フォーカス・矩形選択などの回帰確認。行高／矩形選択の開始場所の期待値を新仕様へ更新。
- computer-useで専用画面を操作。右側余白のクリック、余白のダブルクリックでAだけをフォルダ移動、Home→Ctrl+Shift+Endで500件選択、左端の余白からのドラッグで5件選択し、マウスを離した後も保持することを確認。
- マウスのサイドボタンそのものと、マウスShiftクリックの直接入力はツールから注入していない。XButtonのイベント接続・共通コマンドのA/B独立性、標準のキーボード範囲選択を確認した範囲と区別する。

記録: [変更前](artifacts/input-check-89f16a1ba95c41afbac17c11c872d391/input-check.txt)、[変更後と追加7件](artifacts/input-check-2cd901726d884b13b5cd89500d819e94/input-check.txt)、[実操作の記録](artifacts/input-ui-check-2b65675b9ed04155867e2d79d3cc2609/manual-ui-check.md)。

最終Debugビルドは警告0・エラー0。既存44件の最新の結果は [WinUI統合記録](artifacts/focus-check-0b42389964e7446b8ff5a6a2bb4b506e/focus-check.txt)。

```powershell
dotnet build FilerApp.sln -p:Platform=x64 --no-restore
./scripts/CheckInput.ps1
./scripts/CheckPaneFocus.ps1
```

実装: [選択集計](src/FilerApp/Views/PaneView.Selection.cs)、[行と背景の入力](src/FilerApp/Views/FileListView.cs)、[行の描画](src/FilerApp/Views/FileDetailsRow.cs)、[マウス履歴](src/FilerApp/MainWindow.Mouse.cs)、[選択復元](src/FilerApp/Views/PaneView.Position.cs)。

仕様参照: [MicrosoftのListView選択の説明](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview)、[PointerUpdateKind](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.pointerupdatekind?view=windows-app-sdk-1.8)。
