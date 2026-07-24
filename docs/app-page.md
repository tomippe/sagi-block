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
- **app-keycolor**: **#F0BE38**（Windows アイコンのイエローをやや濃く）
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

1. 概要（トレイ常駐・検知対象）
2. 詐欺警告ページの説明 + 例示画像（メディア **2387**）
3. 詐欺通知の説明 + 例示画像（メディア **2388**）
4. 判定・脅威情報・定期チェック
5. プライバシー・Firefox 未対応

例示画像は本文内 `<img width="700">` で掲載（app-ss01 とは別）。

## 実施済み / 未実施

- [x] 紹介ページ作成（2026.07.06）
- [x] プライバシーポリシーページ作成（2026.07.06）
- [x] app-icon / app-kvbg / app-ss01 アップロード
- [x] Microsoft Store Product identity 取得（Store ID: 9PKH91ZX0W9J）
- [x] MSIX ビルド（windows/build.ps1）
- [x] Microsoft Store 公開（2026.07.11）— 紹介ページ公開済み

## 参考

- [store-metadata.md](store-metadata.md) — Partner Center 用文言
- [behavior.md](behavior.md) — 機能・判定基準
