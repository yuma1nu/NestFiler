# Nest Filer

Windows 用の 2 ペインファイラー。左右独立のタブ、お気に入り、検索、プレビューを備えた **試用版**です。

## ダウンロード・起動

[v0.1.2 試用版をダウンロード](https://github.com/yuma1nu/NestFiler/releases/tag/v0.1.2)

1. リリースの Assets から `NestFiler-win-x64-v0.1.2.zip` をダウンロード。
2. ZIP をローカルの書き込み可能なフォルダに展開。
3. フォルダ内の `NestFiler.exe` を起動。exe だけを取り出さず、同梱ファイルを維持してください。

Windows x64 向け（主対象は Windows 11、対象 API の最小 OS は Windows 10 build 19041）。.NET と Windows App SDK を同梱するポータブル版です。クリーン PC での動作確認は未実施です。

**未署名の試用版です。** Windows の警告が表示される場合があります。ファイルのコピー・移動・ごみ箱操作は実データを変更します。重要なデータはバックアップし、まず検証用フォルダでお試しください。

更新・削除、設定保存先などは [配布・利用手順](DISTRIBUTION.md)、今回の内容は [リリースノート](RELEASE-NOTES.md) を参照してください。

## 主な機能

- 1／2 ペイン、左右独立のタブ、戻る／進む、パンくず、タブ・作業位置の保存。
- 詳細／サムネイル表示、自然順ソート、列幅・順序の変更、複数選択、一覧の絞り込み。
- グループ付きのお気に入り、場所ツリー、最近使ったファイル／フォルダ。
- コピー・切り取り・貼り付け、移動、名前変更、新規フォルダ、ごみ箱、ドラッグ＆ドロップ。
- v0.1.2からサーバー共有・ネットワークドライブの確認付き完全削除に対応（[操作・検証結果](SERVER-DELETION.md)）。
- SharePointのOffice共有リンクを貼り付けて、デスクトップアプリで開くショートカットを作成（v0.1.1から対応。[仕様と検証](SHAREPOINT-SHORTCUTS.md)）。
- ファイル名検索、テキスト検索（正規表現、UTF-8／CP932）、検索結果タブ。
- Space キーで画像・テキスト・Markdown・CSV のプレビュー。
- Windows 標準の右クリックメニュー、フォルダ変更の監視、単一起動。
- v0.1.2からショートカットのファイル転送、お気に入りへのドラッグ登録、控えめな選択色を改善（[変更と検証](SHORTCUT-FAVORITE-FIXES.md)）。

[ショートカットと操作](SHORTCUTS.md) · [基盤機能の仕様](FOUNDATION.md) · [一覧とプレビュー](COLUMNS-PREVIEW.md)

## 既知の制約

- 未署名。インストーラー・自動更新はありません。
- クリーン Windows、共有フォルダの切断・権限異常、実際の DPI／モニター変更、長時間利用の検証は未完了です。
- タブの表示位置復元で、過去の自動確認時に断続的なずれを検出しています。
- 大量ファイルでは全件のメタデータ読み込みが必要です。到達不能な共有の OS 同期 I/O は即時停止できません。
- 詳細な未確認事項は各仕様書の記録を参照してください。過去の日付の検証結果や「Debug のみ」の記載は、その作業時点の記録です。

不具合は [Issues](https://github.com/yuma1nu/NestFiler/issues) に、バージョン・Windows のバージョン・再現手順を添えて報告してください。ログを添付する場合はローカルパス等の個人情報を確認してください。

## 開発・検証

Windows x64、Visual Studio の .NET デスクトップ／WinUI 開発ツール、Windows SDK 10.0.26100 を使用します。.NET SDK は `global.json` で 10.0.401（同 feature band のパッチ許可）に固定しています。初回の NuGet 復元にはネット接続が必要です。

```powershell
dotnet build FilerApp.sln -c Release -p:Platform=x64
dotnet run --project tests/FilerApp.Checks -c Release
./scripts/Publish.ps1
```

配布 ZIP と SHA256 を `artifacts/` に生成します。`src/FilerApp` は WinUI 3 の UI、`src/FilerApp.Core` は列挙・検索・状態保存、`tests/FilerApp.Checks` は自動検証です。

表示名は `Nest Filer`、実行ファイルは `NestFiler.exe`。既存設定を引き継ぐため、内部名と設定保存先 `%LOCALAPPDATA%\FilerApp` は継続しています。
