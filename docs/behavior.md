# 詐欺ブロック 動作・判定基準

Windows 版（SagiBlock）の仕様メモ。実装の正本は `windows/SagiBlock/` 配下のソース。

---

## 概要

トレイ常駐アプリ。30 秒ごとに次を実行する。

1. 偽警告（scareware）らしいブラウザウィンドウを検知して閉じる
2. Chromium 系ブラウザの「通知許可」で、詐欺らしい origin をブロックに変更する

トレイメニュー「今すぐチェック」で即時実行できる。

---

## 偽警告ページの検知

対象ブラウザ: Chrome, Edge, Firefox, Brave, Vivaldi, Internet Explorer, Opera

ウィンドウタイトルと、子ウィンドウの可視テキストを正規化してスコアリングする。

- ブラウザ: スコア 10 以上でタブ閉じ → ウィンドウ閉じ →（スコア 14 以上で）プロセス終了
- 非ブラウザ: スコア 14 以上で WM_CLOSE、16 以上でプロセス終了

「閉じた」と報告するのは、ウィンドウが消えたか、詐欺スコアが閾値未満に下がったときのみ。

---

## 詐欺通知の判定

対象: Chrome / Edge / Brave / Vivaldi / Opera の各プロファイル `Preferences`

```
profile → content_settings → exceptions → notifications
  └─ "https://example.com:443,*" → setting: 1 (許可) のみ対象
```

**通知の本文・タイトルは見ない。** 許可されている origin のホスト名だけを判定する。

### スコアリング（合計 3 点以上で対象）

| 条件 | 点数 |
|---|---|
| 偽セキュリティ系語（security, defender, virus, warning, support など） | +3 |
| 通知スパムのおとり語（quiz, trivia, genius, prize, spin など） | +3 |
| 怪しい TLD（.xyz, .top, .co.in など） | +2 |
| ハイフン 2 個以上 | +1 |
| 数字 4 文字以上 | +1 |
| サブドメイン階層 4 以上 | +1 |
| IP アドレス直打ち | +3 |
| ホスト名 28 文字以上 | +1 |

信頼ドメイン（google.com, amazon.co.jp など）は除外。

### 外部フィード

ローカルスコアが 3 未満のときのみ PhishTank と URLhaus を参照する。

- PhishTank: 日次ダウンロード
- URLhaus: オンデマンド、7 日キャッシュ

### ブロック時の動作

1. 該当ブラウザが起動中なら **終了**（Preferences が上書きされるため）
2. `Preferences` に block を書き込み（初回は `.sagi-block.bak` を作成）
3. セッション復元を無効化し、**新しいタブでブラウザを再起動**（詐欺ページを再表示しない）

---

## ローカライズ

| 言語 | アプリ内表示名（Strings.resx） |
|---|---|
| 日本語 | 詐欺ブロック |
| English | SAGI BLOCK |
| 简体中文 | 诈骗拦截 |

MSIX manifest の DisplayName は `SagiBlock`（Partner Center 予約名・ASCII）。ストア掲載名は Partner Center の各言語 Product name を使用。

---

## ログ

- イベント: `%LOCALAPPDATA%\SagiBlock\events.jsonl`
- 起動ログ: `%LOCALAPPDATA%\SagiBlock\startup.log`

---

## 現状の制限

- ブラウザ拡張機能は未実装（ネイティブのみ）
- URL 直アクセスの検知は限定的（タイトル・可視テキスト・通知 origin のみ）
- Firefox の通知設定変更は未対応（SQLite 経路が必要）
- ブラウザ起動中は Preferences が上書きされることがある（終了後に再チェック）

---

## 参考

- [store-metadata.md](store-metadata.md) — Store 申請用文言
- [../windows/BUILD.md](../windows/BUILD.md) — ビルド手順
