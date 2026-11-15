# Nest Filerへの名称変更

2026-11-15。Debug版を更新。Release版・配布ZIPは更新していない。

## 変更した名前

- ウィンドウのタイトル: `Nest Filer`
- 実行ファイル: `NestFiler.exe`（アセンブリも`NestFiler`）
- ファイルの製品名・説明: `Nest Filer`
- Visual Studioのプロジェクト表示名・起動プロファイル: `Nest Filer`
- 今後のPublishスクリプトの出力フォルダ接頭辞: `NestFiler-win-x64-`

README・配布手順・チェック用スクリプトの起動先も更新した。Debugビルドの増分クリーンにより、同じ出力フォルダの旧`FilerApp.exe`などは除かれ、新しい実行ファイル・DLL・PRI・ランタイム設定が揃っている。

既存のお気に入り・履歴・設定・タブを継続するため、保存先`%LOCALAPPDATA%/FilerApp`、単一起動の識別、内部の名前空間、ソリューションとソースのファイル配置は維持する。設定の移動・初期化は行っていない。候補アイコンは今回適用していない。

## 確認

- `dotnet build FilerApp.sln -p:Platform=x64 --no-restore`: 警告0、エラー0。
- 実際の`NestFiler.exe`のバージョン情報から、FileDescriptionとProductNameがともに`Nest Filer`であることを確認。
- [単一起動の3項目](artifacts/instance-check-3e024bcc914a4e66bc040c24de3703e5): 二重起動時に元のプロセス・ウィンドウを維持、通常終了で設定保存、ロック解放後に同じ設定を再起動。
- [WinUI統合44項目の再実行](artifacts/focus-check-abe553db8aba4030b61865d343db2304/focus-check.txt)は成功。新アセンブリ名でもXAML、一覧、検索、プレビュー、列、Shellメニューなどが動作した。
- 初回のWinUIチェックでは、以前にも差が出たタブの表示位置復元で48 DIPのずれを検知した。[初回記録](artifacts/focus-check-c2fcc3a814ff408ab2e74bb6bd5d860a/focus-check.txt)。同じビルドの再実行では成功したが、断続的な位置復元の問題が解消したとは扱わない。今回の名称変更では位置復元コードを変更していない。
- 通常プロファイルでDebug版を起動し、実際のウィンドウのタイトルが`Nest Filer`、実行パスが`NestFiler.exe`になっていることを確認。

起動:

```powershell
& .\src\FilerApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\NestFiler.exe
```
