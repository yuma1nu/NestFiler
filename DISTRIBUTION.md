# Nest Filer ポータブル版

v0.1.0 / Windows x64向けの未署名試用版。Windows 11を主対象とします。

配布先: https://github.com/yuma1nu/NestFiler/releases/tag/v0.1.0

`NestFiler-win-x64-v0.1.0.zip` と照合用の `.zip.sha256` を公開します。
PowerShell の `Get-FileHash .\NestFiler-win-x64-v0.1.0.zip -Algorithm SHA256` で照合できます。

1. ZIPをローカルの書き込み可能なフォルダに展開。
2. フォルダ内のNestFiler.exeを起動。exeだけを取り出さず、同梱ファイルを維持してください。
3. 更新時はアプリを閉じ、新版を別フォルダに展開して起動。
4. アンインストールはアプリ終了後に展開フォルダを削除。設定を残さない場合だけ `%LOCALAPPDATA%\FilerApp` を別途削除。

.NETとWindows App SDKを自己完結配置しています。端末のOSコンポーネントやVC++ランタイム等を含め、クリーンPCでの動作確認は未実施です。

設定と履歴: `%LOCALAPPDATA%\FilerApp\state.db`。
名称変更後も既存のお気に入り・履歴・タブを引き継ぐため、この保存先を継続します。
エラーログ: 同フォルダの `logs/app.log` と `app.previous.log`（各約1 MB）。エラーに含まれるローカルパス等が記録される場合があります。ファイル本文はログに記録しません。

署名とインストーラーは未提供です。Windows の警告が表示される場合があります。正式配布前に署名、クリーンPC、ネットワーク共有、DPI、ファイル操作異常系の検証が必要です。重要なデータはバックアップし、まず検証用フォルダでお試しください。

開発者向け: `FILERAPP_PROFILE_DIR` 環境変数を設定すると、そのローカルフォルダを独立した設定・ログ保存先として使用します。既存設定を使わない起動検証用です。通常利用では設定不要。共有フォルダを指定しないでください。
