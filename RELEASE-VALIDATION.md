# 試用版の公開確認

## v0.1.5（2026-11-19）

- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: 182 件成功。
- `dotnet build src/FilerApp/FilerApp.csproj -c Debug -p:Platform=x64 --no-restore -o artifacts/check-debug-v0.1.5`: 成功、警告 0・エラー 0。起動中のアプリとは別フォルダで検証。
- 上記ビルドを `-ExecutablePath` に指定し、`CheckCopyDrop.ps1` 19 件、`CheckNavigation.ps1` 4 件、`CheckEnter.ps1` 10 件、`CheckInput.ps1` 7 件、`CheckResponsiveness.ps1` 9 件が成功（合計 49 件）。
- `CheckNavigation.ps1` のライト／ダークのXAML描画画像を確認。タブの閉じるボタン、検索ボタン、ネイティブタイトルバーの配色値も検証。画像はアプリ内容だけで、ネイティブタイトルバーの画面キャプチャではありません。
- **既存回帰の `CheckPaneFocus.ps1` は 33 件成功後、タブ表示位置復元で 1 件失敗。** アンカーのY位置が期待 -20 DIP に対して 28 DIP、保存オフセット 49048 に対して 49000（48 DIPの差）。以降の項目は未実行です。[名称変更時にも検出した既知の断続的な問題](NAMING.md)として記録し、検証を通すための再実行・基準変更は行っていません。
- `scripts/Publish.ps1` でReleaseの自己完結ポータブルZIPを生成。今回検出した制約をリリースノートに追記し、同梱ノート・ZIP・SHA256を更新。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.5.zip`: 最終ZIPで4項目成功。展開後の起動応答、二重起動防止、設定保存、再起動、アプリのエラーログなしを確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.5.0` を確認。

最終ZIPのSHA256:

```text
bbc04129787c51bd4f54d407d73682170b1dc262f64144f203d45ef78384811d
```

画面検証の記録:

- `artifacts/copy-drop-check-45343540850e49648d120bf3db5b1c97/copy-drop-check.txt`
- `artifacts/navigation-pill-8cbee27d78cf493887f873e968e0d9b7/navigation-preview.txt`
- `artifacts/enter-check-5dea11ee1a07489aa23bd6095f87e8fe/enter-check.txt`
- `artifacts/input-check-9fb3316c65aa408e885c5ec60a68e5d0/input-check.txt`
- `artifacts/responsiveness-check-30d5983e0cba42118b984cf96af9f400/responsiveness-check.txt`
- `artifacts/focus-check-923bf49e997948c086ca4e6cff0264cf/focus-check.txt`（失敗を含む）

専用データ・隔離プロファイルで確認。OSからの実ドラッグ入力、Explorerとの往復ドラッグ、実ハイコントラスト切替、実SMB共有の異常系、認証済みOffice文書、クリーンPC等の未検証事項は継続します。

## v0.1.4（2026-11-18）

- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: 同名衝突・同一フォルダの複製・実Shell操作を含む 182 件成功。
- `dotnet build src/FilerApp/FilerApp.csproj -c Debug -p:Platform=x64 --no-restore -o artifacts/check-debug-v0.1.4`: 成功、警告 0・エラー 0。起動中のアプリとは別フォルダで検証。
- 上記ビルドを `-ExecutablePath` に指定し、`CheckCopyDrop.ps1` 15 件、`CheckEnter.ps1` 10 件、`CheckInput.ps1` 7 件、`CheckResponsiveness.ps1` 9 件、`CheckPaneFocus.ps1` 44 件が成功。
- `scripts/Publish.ps1`: Release の自己完結ポータブル ZIP と SHA256 を生成。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.4.zip`: 4 項目成功。ZIP 展開後の起動応答、二重起動防止、設定保存、再起動、アプリのエラーログなしを確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.4.0` を確認。

ZIP の SHA256:

```text
03ffc89b79899bfb8d63631cb659086bde2e248d3d27569a6ee570085e61ef1b
```

画面検証の記録:

- `artifacts/copy-drop-check-ac8ff05b6bb34f6892d4e3b7d0664f97/copy-drop-check.txt`
- `artifacts/enter-check-4145577f71af44cd806ca237b656cc15/enter-check.txt`
- `artifacts/input-check-aa2dbdcf33de4f12901560d4314ef2e9/input-check.txt`
- `artifacts/responsiveness-check-ed8d3b214b514dc9b8ac3acc07af98d8/responsiveness-check.txt`
- `artifacts/focus-check-13b1246869334b588aa7e7125daba87d/focus-check.txt`

専用データ・隔離プロファイルで確認。ドロップとEnterは実WinUIの保存先・フォーカス判定とアプリ処理を検証し、実際のマウス／キー入力からイベント発火までの全経路や長押しの実入力を代替しません。タイトルバーの外観・実ハイコントラスト切替、クリーンPC、実SMB共有の同名衝突・切断は未確認です。今回のタブ位置復元確認は成功しましたが、過去の断続的なずれの解消を意味しません。

## v0.1.3（2026-11-17）

- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: 166 件成功。
- `dotnet build src/FilerApp/FilerApp.csproj -c Debug -p:Platform=x64 --no-restore -o artifacts/check-debug-v0.1.3`: 成功、警告 0・エラー 0。起動中のアプリとは別フォルダで検証。
- 上記ビルドを `-ExecutablePath` に指定し、`CheckMiddleMouse.ps1` 8 件、`CheckInput.ps1` 7 件、`CheckSharePoint.ps1` 31 件、`CheckPaneFocus.ps1` 44 件が成功。
- `scripts/Publish.ps1`: Release の自己完結ポータブル ZIP と SHA256 を生成。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.3.zip`: 4 項目成功。ZIP 展開後の起動応答、二重起動防止、設定保存、再起動、アプリのエラーログなしを確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.3.0` を確認。

ZIP の SHA256:

```text
03609ff1646a28ec93694ef0226094ddacfd9181568287234b3e312defec2761
```

画面検証の記録:

- `artifacts/middle-mouse-check-dc2f1496fac04b8ab65abdfe77b11930/middle-mouse-check.txt`
- `artifacts/input-check-9669f36024b54fdf82ed440458a32331/input-check.txt`
- `artifacts/sharepoint-check-e2a76c783d2c49b5879e0ea1ca84eb05/sharepoint-check.txt`
- `artifacts/focus-check-585425e669104a80a5a9a931e37005bc/focus-check.txt`

専用データ・隔離プロファイルで確認。中クリックの統合チェックはアプリの処理を呼び出すもので、実際の PointerPressed／Released や、押したまま移動して取り消すジェスチャーの実機確認は未完了です。今回のタブ位置復元確認は成功しましたが、過去の断続的なずれの解消を意味しません。クリーン PC、実 SMB 共有・認証済み Office 文書等の制約も継続します。

## v0.1.2（2026-11-16）

- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: ショートカット本体の操作・完全削除の確認と拒否・キャンセル・部分失敗を含む 166 件成功。
- `dotnet build src/FilerApp/FilerApp.csproj -c Debug -p:Platform=x64 --no-restore -o artifacts/check-debug-v0.1.2`: 成功、警告 0・エラー 0。起動中のアプリと出力先を分離。
- 上記ビルドを `-ExecutablePath` に指定し、`CheckSharePoint.ps1` 31 件、`CheckDeletion.ps1` 8 件、`CheckResponsiveness.ps1` 9 件、`CheckPaneFocus.ps1` 44 件が成功。
- `scripts/Publish.ps1`: Release の自己完結ポータブル ZIP と SHA256 を生成。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.2.zip`: 4 項目成功。ZIP 展開後の起動応答、二重起動防止、設定保存、再起動、アプリのエラーログなしを確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.2.0` を確認。

ZIP の SHA256:

```text
a58ee5f3a8992fd5f7e450db04d8bd75aed864f9fcb557ffbb6a936d0251c853
```

画面検証の記録:

- `artifacts/sharepoint-check-6f397733fdbf46219144b6f7eb6bb3d9/sharepoint-check.txt`
- `artifacts/deletion-check-13ab2092502e4c879a6cb5805d222505/deletion-check.txt`
- `artifacts/responsiveness-check-715993fb0b9949f698f7b3b7bbd88239/responsiveness-check.txt`
- `artifacts/focus-check-16951db35c1d42728618a0a1c078956a/focus-check.txt`

専用の一時データ・隔離プロファイルで確認。完全削除はローカルの試験ファイルをネットワーク分類として扱い、実処理を検証しています。実際の SMB 共有・割り当てドライブ上の削除、通信切断、サーバー権限不足、Explorer との手動往復転送、認証済み Office 文書、クリーン PC の検証は未実施です。今回のタブ位置復元確認は成功しましたが、過去に検出した断続的なずれの解消を意味しません。

## v0.1.1（2026-11-16）

- `dotnet build FilerApp.sln -c Release -p:Platform=x64 --no-restore`: 成功、警告 0・エラー 0。
- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: SharePoint 追加分を含め 144 件成功。
- `dotnet build src/FilerApp/FilerApp.csproj -c Debug -p:Platform=x64 --no-restore -o artifacts/check-debug-v0.1.1`: 成功、警告 0・エラー 0。起動中の通常 Debug 版と出力先を分けて検証。
- `scripts/CheckSharePoint.ps1 -ExecutablePath ./artifacts/check-debug-v0.1.1/NestFiler.exe`: 16 件成功。
- `scripts/CheckPaneFocus.ps1 -ExecutablePath ./artifacts/check-debug-v0.1.1/NestFiler.exe`: 44 件成功。
- `scripts/Publish.ps1`: Release の自己完結ポータブル ZIP と SHA256 を生成。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.1.zip`: 4 項目成功。ZIP 展開後の起動、二重起動防止、設定保存、再起動、アプリのエラーログなしを確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.1.0` を確認。

ZIP の SHA256:

```text
13437a7443a11ea53d45e4b89499c73d1d496b6e8d9071dc7cfc6deaa4e8681d
```

テストは専用データ・隔離プロファイルと架空の SharePoint URL を使用。実際の認証済み文書の Office 起動、クリーン PC、共有フォルダ・DPI・長時間利用は未検証です。

## v0.1.0

2026-11-16、開発 PC（Windows x64）で実施。

- `dotnet build FilerApp.sln -c Release -p:Platform=x64 --no-restore`: 成功、警告 0・エラー 0。
- `dotnet run --project tests/FilerApp.Checks -c Release --no-restore`: 111 件成功。ファイル操作は専用の一時データで実施。
- `scripts/Publish.ps1`: Release の自己完結ポータブル ZIP と SHA256 を生成。
- `scripts/CheckPortable.ps1 -ZipPath ./artifacts/NestFiler-win-x64-v0.1.0.zip`: 4 項目成功。ZIP 展開後の実行ファイル・XAML リソース・ドキュメント、起動応答、二重起動抑止、正常終了と設定保存、保存済みの独立プロファイルでの再起動を確認。
- 配布 exe の製品名 `Nest Filer` とファイルバージョン `0.1.0.0` を確認。

ZIP の SHA256:

```text
28c1ba249cef8a726ae2768a3a72caf6fbbd61022465a60f9d23bea2750a7863
```

この確認はクリーン PC、全操作の GUI 視覚確認、共有フォルダ・DPI・長時間試験を代替しません。[リリースノート](RELEASE-NOTES.md)の制約を継続します。
