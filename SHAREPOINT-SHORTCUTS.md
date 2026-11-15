# SharePoint共有リンクの貼り付け（2026-11-16 / Debug）

## 操作

1. SharePointでOfficeファイルの「リンクをコピー」を実行。
2. Nest Filerで保存先のA/Bペインをクリックし、Ctrl+V、Shift+Insert、または独自右クリックメニューの「貼り付け」。
3. 例として `実験報告.pptx.url` を作成。リンクが名前を含まない場合は名前を入力し、アプリの種類も不明ならWord／Excel／PowerPointから選択。
4. ダブルクリックまたはEnterでOfficeデスクトップアプリを呼び出す。貼り付け時にはファイルを開かず、ダウンロードもしない。

Ctrl+Lやパス欄クリックでアドレス入力中の場合は、Ctrl+V／Shift+Insertは通常の文字列貼り付けになる。検索入力や名前入力など、ほかの編集欄も同様。検索結果タブにはショートカットを作成できない。

ファイルのコピー／切り取り情報がクリップボードに含まれる場合は、それを最優先して従来のファイル貼り付けを行う。共有リンクの貼り付けではユーザーのクリップボードを書き換えない。

## 実装

- `SharePointLink` はHTTPSのSharePoint Online共有リンクを解釈。`:w:`／`:x:`／`:p:`、パス末尾のOffice拡張子、`Doc.aspx`の`file`パラメータから種類・名前を判断する。認証付きページを取得して名前を調べる処理は行わない。
- 対応ホスト末尾は `.sharepoint.com`、`.sharepoint.us`、`.sharepoint.cn`、`.sharepoint.de`、`.sharepoint-mil.us`。社内独自ドメインのSharePoint Server、一般Webリンク、フォルダ共有、PDFなどは対象外としてメッセージを表示。
- [MicrosoftのOffice URI仕様](https://learn.microsoft.com/en-us/office/client-developer/office-uri-schemes)に従い、`ms-word:ofe|u|…`、`ms-excel:ofe|u|…`、`ms-powerpoint:ofe|u|…` を `.url` の起動先に保存。共有URLのクエリ文字列・共有トークンを保持し、URI内の縦棒はエスケープする。Officeへの編集モード要求であり、編集権限を付与するものではない。
- `SharePointShortcutService` がUIスレッド外で作成。WindowsのInternetShortcut形式（UTF-16 BOM付き）を使用。種類に対応する登録済みアイコンを取得できればIconFile／IconIndexにも設定する。
- 一時ファイルに書き込みを完了してから、上書き禁止の移動で公開。同名なら `実験報告 (2).pptx.url` のように連番化する。同時貼り付けでも既存ファイルを上書きしない。キャンセル・到達不能・作成失敗を成功扱いしない。
- 貼り付け開始時の保存先を保持。非同期処理中に別ペインを選択しても保存先を変更せず、完了時にフォーカスを奪わない。
- 一覧は `.pptx.url` などの二重拡張子からOffice関連付けアイコンと「PowerPoint リンク」等の種類を表示。スクロール中にショートカット本体やリンク先へアクセスしない。この名前規則による表示判定は既存の同名形式の `.url` にも適用される。
- リンク本体を開く処理は既存のWindows Shell関連付け起動を使用。Office未登録の場合は作成完了時にインストールが必要と表示し、ブラウザ起動先には変更しない。

`.url` は共有先へアクセスできるリンク情報を含むため、共有権限に合わせて取り扱う。クラウドへのアップロードや認証情報の外部送信は実装していない。

## 検証結果

- `dotnet build FilerApp.sln -p:Platform=x64 --no-restore`：成功、警告0／エラー0。
- `dotnet run --project tests/FilerApp.Checks --no-restore`：144件成功。今回33件追加。日本語、Office種類、URL検証、不透明リンク、予約名、同時作成・衝突、作成前キャンセル、到達不能な保存先、Windows標準COMのInternetShortcutによるOffice起動URIの読み取りを確認。
- `./scripts/CheckSharePoint.ps1`：16件成功。A/B、Text／WebLink、アドレス編集時のキー振り分け・作成防止、名前指定キャンセル、非同期完了時の保存先・フォーカス、StorageItems優先、連番、検索タブの作成防止を隔離プロファイルで確認。
- 統合テストはユーザーのシステムクリップボードを変更しない。アドレス欄の検証は実TextBoxへのフォーカス、Ctrl+V／Shift+Insertを横取りしない判定、選択文字列への挿入まで。実際のキー入力とクリップボードを通した手動操作は別途確認が必要。
- `./scripts/CheckPaneFocus.ps1`：既存44件成功。タブ、ペイン、列、プレビュー、検索、既存ファイルのコピー／移動・部分失敗等の回帰確認。

統合テスト記録：

- `artifacts/sharepoint-check-6107530cedbb4b919963e499d152ee3b/sharepoint-check.txt`
- `artifacts/focus-check-6949c2b8c12b48589f7c4673889152bc/focus-check.txt`

テストは専用の一時フォルダ／隔離プロファイルと架空の共有URLのみ使用。実際の認証済みSharePoint文書の起動は未検証。Officeのインストール、サインイン、共有権限、組織ポリシー、コピーされたリンク形式により起動結果が変わる可能性がある。特に不透明な共有URLのOffice側対応は実リンクで確認する必要がある。Office以外のファイルのデスクトップ起動、オンプレミスSharePoint、すべての組織クラウドでの実機確認は未対応／未確認。

上記は機能追加時のDebug版の検証記録です。公開試用版 v0.1.1 にもこの機能を含めています。配布版の検証は [RELEASE-VALIDATION.md](RELEASE-VALIDATION.md) を参照してください。
