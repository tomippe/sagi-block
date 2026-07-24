# Microsoft Store 申請用メタデータ

Partner Center で入力する文言を 3 言語分まとめる。コピペ用のため引用符・表罫線は使わず、見出しの直下に本文のみ置く。

---

## 基本情報

プライバシーポリシー URL
[https://apps.tomippe.jp/sagi-block/policy/](https://apps.tomippe.jp/sagi-block/policy/)

カテゴリ
セキュリティ / Security

著作権
Copyright © 2026 tomippe. All rights reserved.

データ収集（個人データ）
いいえ（収集しない）

---

## Product identity（manifest と一致させる）

正本: `windows/SagiBlock/Package.appxmanifest`

Package/Identity/Name
StudioTomippe.19837858EB356

Package/Identity/Publisher
CN=7AA9757D-D72F-4DE2-9980-F9A659207C27

Package/Properties/DisplayName
SagiBlock

Package/Properties/PublisherDisplayName
Studio Tomippe

Package Family Name (PFN)
StudioTomippe.19837858EB356_jrvvt7wkhq9ve

Store URL
[https://apps.microsoft.com/detail/9PKH91ZX0W9J](https://apps.microsoft.com/detail/9PKH91ZX0W9J)

Store ID
9PKH91ZX0W9J

MSA app Id
774a5159-4a07-4d2e-8765-f059f0f6e9fc

### Manage app names（予約名）

詐欺ブロック — 日本語ストア掲載名
SagiBlock — manifest の DisplayName（ASCII のみ。日本語を manifest に入れると MSIX 化時に文字化けする）
诈骗拦截 — 中国語ストア掲載名

Identity Version の第 4 桁は常に 0（例: version.txt が 0.1.3 なら manifest は 0.1.3.0）

提出パッケージ
windows/publish/msix/SagiBlock.msixbundle

---



## Restricted capabilities（Partner Center 申告）

manifest（`Package.appxmanifest`）で宣言している **Restricted capabilities は 2 つ**。Partner Center の提出フォームでもこの 2 つを選び、理由欄に下記英語をコピペする。


| 種別         | 名前                     | manifest          | Partner Center で申告    |
| ---------- | ---------------------- | ----------------- | --------------------- |
| Restricted | runFullTrust           | rescap:Capability | **要**                 |
| Restricted | unvirtualizedResources | rescap:Capability | **要**                 |
| 通常         | internetClient         | Capability        | 申告不要（Restricted ではない） |




### runFullTrust — 理由（英語・コピペ用）

The app is a full-trust desktop tray utility (EntryPoint: Windows.FullTrustApplication). It must enumerate top-level windows owned by other processes, read window titles and visible text to detect fake virus-warning pages, send WM_CLOSE / CloseMainWindow to close those browser windows, and in some cases terminate a browser process tree when notification settings must be updated safely. These cross-process operations are not available to sandboxed Store apps.

### unvirtualizedResources — 理由（英語・コピペ用）

**Partner Center の Restricted capability 理由欄は 500 文字上限。** 下記「短縮版」のみ貼る。unvirtualizedResources の詳細と Notes for Certification 全文は正本 `windows/scripts/store-cert-notes-en.txt`（`store-submit.ps1` が API 提出時に自動設定）。

#### 短縮版（500文字以内・Partner Center 理由欄用）

SagiBlock writes real Chromium Preferences JSON to block scam notifications (Chrome, Edge, Brave, Vivaldi, Opera). Virtualized paths fail. Scope: browser Preferences, optional .sagi-block.bak, session-restore cleanup; LOCALAPPDATA\SagiBlock logs/caches only. MSIX: StartupTask, no registry. Flexible Virtualization cannot modify other apps profiles. Policy discloses profile changes and post-uninstall data: https://apps.tomippe.jp/sagi-block/policy/

（450 文字）

### 通常 capability（参考・申告不要）

internetClient — downloads public threat intelligence feeds only (PhishTank, URLhaus) to evaluate notification origin hostnames. No user content or personal identifiers are sent.

---



## Partner Center リスト登録（3言語）



### アプリ名（Product name）

日本語
詐欺ブロック

英語
SagiBlock

簡体中国語
诈骗拦截

### 短い説明（Short description）

日本語
偽のウイルス警告ページを自動で閉じ、詐欺サイトのブラウザ通知許可をオフにする常駐アプリ。30秒ごとにバックグラウンドで監視します。

英語
A tray app that closes fake virus-warning pages and blocks scam site notification permissions. Runs a background check every 30 seconds.

簡体中国语
常驻托盘应用：自动关闭虚假病毒警告页面，并关闭诈骗网站的通知权限。每 30 秒在后台检查一次。

### 説明文（Full description）

日本語
詐欺ブロックは、Windows 用のトレイ常駐セキュリティユーティリティです。偽の Microsoft Defender 警告やサポート詐欺のページを検知してウィンドウを閉じ、Chrome・Edge・Brave・Vivaldi・Opera などで「許可」になっている詐欺らしい Web 通知の origin をブロックに変更します。

ブラウザのウィンドウタイトルと表示テキストをもとに、詐欺警告らしいページをスコアリングして閉じます。Chromium 系ブラウザの通知設定はプロファイルの Preferences を読み取り、怪しいドメインはまずローカル判定し、必要に応じて公開の脅威情報（PhishTank・URLhaus）も参照します。通知設定を書き込む際、ブラウザが起動中だと上書きされることがあるため、該当ブラウザを一度終了してから設定を保存し、新しいタブで再起動します。

トレイアイコンから「今すぐチェック」で手動スキャンもできます。個人を特定するデータは収集せず、ログは端末内（%LOCALAPPDATA%\SagiBlock）にのみ保存します。Firefox の通知設定変更には未対応です。

英語
SagiBlock is a tray-based security utility for Windows. It detects fake Microsoft Defender-style warning pages and support-scam screens, then closes those browser windows. It also scans notification permissions in Chrome, Edge, Brave, Vivaldi, and Opera, and changes suspicious allowed origins to blocked.

The app scores open browser windows by title and visible text, then closes high-risk pages. For Chromium browsers, it reads each profile’s Preferences file, applies local hostname scoring first, and consults public threat feeds (PhishTank and URLhaus) when needed. If a browser is running while notification settings are updated, the browser may overwrite changes; the app closes that browser first, writes the block, then restarts it with a new tab.

Use Check Now from the tray menu for an immediate scan. The app does not collect personally identifiable data; logs stay on your device only (%LOCALAPPDATA%\SagiBlock). Firefox notification cleanup is not supported yet.

簡体中国语
诈骗拦截是一款 Windows 托盘常驻安全工具。它会检测并关闭伪造 Microsoft Defender 警告、技术支持诈骗等页面，并扫描 Chrome、Edge、Brave、Vivaldi、Opera 等浏览器中已“允许”的疑似诈骗网站通知来源，将其改为阻止。

应用根据浏览器窗口标题和可见文本进行评分并关闭高风险页面。对于 Chromium 内核浏览器，会读取各配置文件的 Preferences，先进行本地域名评分，必要时参考公开威胁信息（PhishTank、URLhaus）。写入通知设置时，若浏览器正在运行可能会被覆盖，因此应用会先结束该浏览器，写入阻止设置后，以新标签页重新启动。

可从托盘菜单选择“立即检查”进行手动扫描。不收集可识别个人的数据，日志仅保存在本机（%LOCALAPPDATA%\SagiBlock）。暂不支持 Firefox 通知设置。

### What's new in this version（v0.1.3・初回）

日本語
初回リリースです。偽警告ページの自動検知・閉じ、詐欺通知許可のブロック、トレイ常駐での定期チェックに対応します。

英語
Initial release. Adds automatic detection and closing of fake warning pages, blocking of scam notification permissions, and periodic tray-based checks.

簡体中国语
首次发布。支持自动检测并关闭虚假警告页面、阻止诈骗通知权限，以及托盘常驻定期检查。

### Product features（製品の特徴）

日本語
• 偽ウイルス警告・サポート詐欺らしいブラウザページを自動で閉じる
• Chrome / Edge / Brave / Vivaldi / Opera の詐欺らしい通知許可をブロック
• 30秒ごとのバックグラウンドチェックと、手動チェック
• ローカル判定に加え PhishTank・URLhaus でドメインを確認
• 個人データを収集しない。処理とログは端末内で完結

英語
• Automatically closes browser pages that look like fake virus warnings or support scams
• Blocks suspicious notification permissions in Chrome, Edge, Brave, Vivaldi, and Opera
• Background checks every 30 seconds, plus manual Check Now
• Uses local scoring plus PhishTank and URLhaus for suspicious domains
• No personal data collection; processing and logs stay on your device

簡体中国语
• 自动关闭疑似虚假病毒警告、技术支持诈骗的浏览器页面
• 阻止 Chrome / Edge / Brave / Vivaldi / Opera 中疑似诈骗的通知权限
• 每 30 秒后台检查，并支持手动立即检查
• 本地评分结合 PhishTank、URLhaus 验证可疑域名
• 不收集个人数据，处理与日志均在设备本地完成

---



## Notes for Certification（審査員向け・英語）

正本: `windows/scripts/store-cert-notes-en.txt`（このファイルだけを編集する）

- Partner Center 手動入力: 上記 txt の全文を Notes for Certification にコピペ
- API 提出: `store-submit.ps1` が同ファイルを `notesForCertification` に自動設定
- unvirtualizedResources の capability 理由欄（500 文字上限）: 本ドキュメント「Restricted capabilities → unvirtualizedResources → 短縮版」を使用

---



## プライバシーポリシー追記（審査 10.6.3 対応・2026-07-09）

WordPress 投稿 ID: 2385（[https://apps.tomippe.jp/sagi-block/policy/）](https://apps.tomippe.jp/sagi-block/policy/）)
Partner Center の Privacy policy URL と一致させる。以下を「データの保存」の後に追加する。

### ブラウザプロファイルへの変更

本アプリは、詐欺らしい Web 通知をブロックするため、Chromium 系ブラウザ（Chrome、Edge、Brave、Vivaldi、Opera 等）の各プロファイルにある Preferences ファイルを読み取り、通知許可（Allow）になっている origin のうち疑わしいものを Block に変更します。初回変更時のみ、同じフォルダに Preferences.sagi-block.bak というバックアップを作成することがあります。安全にブラウザを再起動するため、同じプロファイルフォルダ内のセッション復元用ファイル（Current Session、Sessions フォルダ内のファイル等）を削除し、Preferences 内の起動設定（restore_on_startup 等）を調整することがあります。Cookie、閲覧履歴、パスワード、ブックマーク等のその他のブラウザデータにはアクセスしません。

### アンインストール後に残るデータ

Microsoft Store 版をアンインストールしても、以下は自動では削除されません。不要であればユーザーが手動で削除できます。

- %LOCALAPPDATA%\SagiBlock\ フォルダ（イベントログ startup.log、events.jsonl、公開脅威情報のキャッシュ等）
- ブラウザプロファイル内に作成した Preferences.sagi-block.bak
- Preferences に書き込んだ通知ブロック設定（ブラウザ側の設定として残ります。詐欺通知を防ぐための意図的な動作です）

---



## 申請前チェックリスト

- [ ] version.txt と Package.appxmanifest の Version を一致させる（第 4 桁は 0）
- [ ] manifest の Identity / DisplayName / Publisher が Product identity と一致（DisplayName は SagiBlock）
- [x] プライバシーポリシー URL を Partner Center に設定する（ページ公開済み。Partner Center への入力は申請時）
- [ ] ストアの Product name・説明・短い説明に上記 3 言語を登録する
- [ ] Restricted capabilities: runFullTrust 理由は本ドキュメント全文、unvirtualizedResources 理由は **短縮版（500文字以内）** を貼る
- [ ] Notes for Certification: `windows/scripts/store-cert-notes-en.txt` を Partner Center に貼る（API 提出時は store-submit.ps1 が自動設定）
- [ ] プライバシーポリシーに「ブラウザプロファイルへの変更」「アンインストール後に残るデータ」を追記済み
- [ ] データ収集の質問で「個人データを収集しない」を選択する
- [ ] 提出用 MSIX は windows で .\build.ps1 実行後の publish\msix\SagiBlock.msixbundle を使用する

---



## 参考

- [app-page.md](app-page.md) — apps.tomippe.jp 紹介ページ設定
- [behavior.md](behavior.md) — 動作・判定基準
- [../windows/BUILD.md](../windows/BUILD.md) — ビルド手順

