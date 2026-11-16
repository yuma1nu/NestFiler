# 試用版の公開確認

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
