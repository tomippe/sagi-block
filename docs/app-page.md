# 詐欺ブロック 紹介ページ設定（apps.tomippe.jp）

## キャッチフレーズ（app-cp）

偽警告ページを閉じ、詐欺通知を止める
トレイ常駐で30秒ごとに自動チェック

## プラットフォーム

- **platform**: ["win"]
- **app-winurl**: https://apps.microsoft.com/detail/9PKH91ZX0W9J?hl=ja-JP&gl=JP
- **app-windesc**: Windows 10+, Store<br>日本語,English,中文

## KV背景・キー色

- **app-kvbg**: **2383**（パトライト KV 写真・ユーザー提供）
- **app-keycolor**: **#FED45B**（Windows アイコンのイエロー）
- **app-kvbgaddcss**:
  background-repeat: no-repeat;
  background-position: center;
  background-size: cover;
  background-blend-mode: luminosity;

## WordPress / .env

- **WP_APP_POST_ID**: **2381**
- **WP_APP_PAGE_URL**: https://apps.tomippe.jp/sagi-block/
- **WP_POLICY_POST_ID**: **2385**
- **WP_POLICY_PAGE_URL**: https://apps.tomippe.jp/sagi-block/policy/
- **app-icon**: **2382**（`windows/SagiBlock/Assets/app-icon.png`）
- **app-ss01**: **2386**（IPA 体験サイト検知トースト・1024×507）
- **app-ss01width**: **700**

## 本文（content）メモ

段落は 1 話題 1 `<p>`。バージョン履歴は app-versions に入れ、本文には書かない。

詐欺ブロックは、Windows 向けのトレイ常駐セキュリティユーティリティです。偽のウイルス警告やサポート詐欺のページを検知して閉じ、Chrome・Edge・Brave・Vivaldi・Opera などで許可されている詐欺らしい Web 通知をブロックします。

30 秒ごとにバックグラウンドでチェックします。トレイメニューの「今すぐチェック」で手動スキャンもできます。

個人データは収集しません。ログは端末内（%LOCALAPPDATA%\SagiBlock）にのみ保存されます。

## 実施済み / 未実施

- [x] 紹介ページ作成（2026.07.06）
- [x] プライバシーポリシーページ作成（2026.07.06）
- [x] app-icon / app-kvbg / app-ss01 アップロード
- [x] Microsoft Store Product identity 取得（Store ID: 9PKH91ZX0W9J）
- [x] MSIX ビルド（windows/build.ps1）

## 参考

- [store-metadata.md](store-metadata.md) — Partner Center 用文言
- [behavior.md](behavior.md) — 機能・判定基準
