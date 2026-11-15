# v0.1.0 試用版の公開確認

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
