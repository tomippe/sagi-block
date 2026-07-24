# Microsoft Store 提出 — 作業指示書（詐欺ブロック）

Partner Center への MSIX 提出手順。掲載文言は [store-metadata.md](store-metadata.md)、ビルド詳細は [windows/BUILD.md](../windows/BUILD.md)。

---

## 全体の流れ

1. **初回のみ** … `~/.msstore-env`（Entra + 署名 pfx）と `.env`（Store ID）を設定
2. **Windows PC** … `windows\build.ps1` で署名済み MSIX bundle を生成
3. **提出** … `build.ps1 -Publish` または `store-submit.ps1`（StoreBroker API）
4. **Mac** … `./build.sh -cm "…"` で manifest 同期・版上げ・Git（Store 審査通過後）

---

## 使うアカウント（重要）

| 用途 | アカウント |
|------|------------|
| Partner Center Web（手動操作） | `tomi@tomippe.jp` でも `@onmicrosoft.com` でも可 |
| Entra / アプリ登録 / API 提出 | **`tomi@tomippe.onmicrosoft.com`**（Studio Tomippe テナント） |

`tomi@tomippe.jp` は MSA（個人アカウント）のため、Entra や Store API には使えない。

Entra のアプリ登録は **POUCHES Store Collections**（Client ID: `d1a9a0e4-cdd1-4e1b-9cd6-587de603926a`）を **Studio Tomippe 共通** で使う。詐欺ブロック専用に新規作成しない。

---

## 初回セットアップ

### 1. Mac — `~/.msstore-env`

正本の雛形: `build-common/msstore-env.example`

```bash
chmod 600 ~/.msstore-env
```

必須項目:

```
export MSSTORE_TENANT_ID=（Entra 概要のテナント ID）
export MSSTORE_SELLER_ID=（Partner Center → アカウント設定）
export MSSTORE_CLIENT_ID=d1a9a0e4-cdd1-4e1b-9cd6-587de603926a
export MSSTORE_CLIENT_SECRET=（Entra で新規発行した Secret の値）

export MS_STORE_SIGNING_PFX=/Users/tomippe/Cursor/pouches/native/windows/signing/StudioTomippe-MSIX.pfx
export MS_STORE_SIGNING_PFX_PASSWORD=（pfx パスワード）
export MS_STORE_PUBLISHER_ID=CN=7AA9757D-D72F-4DE2-9980-F9A659207C27
export MS_STORE_PUBLISHER_DISPLAY_NAME=Studio Tomippe
```

Client Secret は Entra で再表示できない。紛失したら同アプリで新規発行する。

Partner Center → アカウント設定 → ユーザー管理 → **Azure AD アプリケーション** に上記 Client ID が **マネージャー** で登録されていることを確認。

### 2. Windows PC — `%USERPROFILE%\.msstore-env`

Mac と **同じ Entra 情報**。PFX パスだけ Windows 向けにする。

```
MS_STORE_SIGNING_PFX=Y:\Cursor\pouches\native\windows\signing\StudioTomippe-MSIX.pfx
MS_STORE_SIGNING_PFX_PASSWORD=（Mac と同じ）
MSSTORE_TENANT_ID=...
MSSTORE_SELLER_ID=...
MSSTORE_CLIENT_ID=d1a9a0e4-cdd1-4e1b-9cd6-587de603926a
MSSTORE_CLIENT_SECRET=...
```

`export` 付き（Mac 形式）でも `build.ps1` は読み込める。

PFX 未設定時は `build.ps1` が `..\pouches\native\windows\signing\StudioTomippe-MSIX.pfx` を自動参照する。

### 3. プロジェクト `.env`

```
MS_STORE_PRODUCT_ID=9PKH91ZX0W9J
MS_STORE_PACKAGE_NAME=StudioTomippe.19837858EB356
MS_STORE_PUBLISHER_ID=CN=7AA9757D-D72F-4DE2-9980-F9A659207C27
MS_STORE_PUBLISHER_DISPLAY_NAME=Studio Tomippe
MS_STORE_PACKAGE_PATH=windows/publish/msix/SagiBlock.msixbundle
```

---

## Windows — ビルドと提出

作業ディレクトリ: リポジトリの **`windows`** フォルダ。

### 前提確認

```powershell
dotnet --version          # .NET 8
# makeappx.exe / signtool.exe … Windows 10/11 SDK
Test-Path $env:USERPROFILE\.msstore-env   # True
```

### 通常ビルド（EXE + 署名済み MSIX）

```powershell
cd Y:\Cursor\sagi-block\windows
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

成果物:

```
publish\msix\SagiBlock.msixbundle    ← Partner Center アップロード用
publish\msix\SagiBlock_x64.msix
publish\msix\SagiBlock_arm64.msix
publish\exe\x64\SagiBlock.exe
publish\exe\arm64\SagiBlock.exe
```

### Store へ API 提出

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Publish
```

または、ビルド済み bundle がある場合:

```powershell
.\scripts\store-submit.ps1
```

初回は StoreBroker モジュールのインストールを求められることがある（CurrentUser スコープ）。

### その他オプション

```powershell
.\build.ps1 -Clean      # publish 削除後フルビルド
.\build.ps1 -Exe        # EXE のみ（MSIX なし）
```

---

## Mac — 提出（Windows ビルド後）

Windows で bundle を作ったあと、Mac から API 提出も可能。

```bash
cd /Users/tomippe/Cursor/sagi-block
source ~/.msstore-env && source .env
./scripts/msstore-publish.sh
```

`msstore` CLI 未インストール時:

```bash
brew install microsoft/msstore-cli/msstore-cli
```

Windows の `build.ps1 -Publish`（StoreBroker）と同等の目的。どちらか一方でよい。

---

## 版上げと manifest 同期（審査通過後）

Store 申請・版反映後、Mac で:

```bash
cd /Users/tomippe/Cursor/sagi-block
./build.sh -cm "変更内容の要約"
```

- `windows/version.txt` パッチ +1
- `apps.tomippe.jp/sagi-block/manifest.json` の `win_version` 更新・FTP
- Git コミット

---

## 提出前チェックリスト

- [ ] `windows/version.txt` と MSIX Identity Version が一致（第 4 桁は常に 0。例: 0.1.3 → 0.1.3.0）
- [ ] manifest DisplayName は **SagiBlock**（ASCII のみ）
- [ ] [store-metadata.md](store-metadata.md) の Product identity と manifest が一致
- [ ] Restricted capabilities（runFullTrust / unvirtualizedResources）を Partner Center で申告済み
- [ ] プライバシーポリシー URL 設定済み: https://apps.tomippe.jp/sagi-block/policy/
- [ ] `publish\msix\SagiBlock.msixbundle` が **署名済み**（build.ps1 成功ログに signed と出る）
- [ ] What's New は大きな機能・重大不具合の版だけ更新（軽微版では前版を維持）

---

## トラブルシュート

| 症状 | 対処 |
|------|------|
| `tomi@tomippe.jp` で Entra に入れない | `@onmicrosoft.com` を使う |
| `MS_STORE_SIGNING_PFX not found` | `%USERPROFILE%\.msstore-env` の PFX パスを確認。または pouches の pfx を参照 |
| `SB_*` / `MSSTORE_*` 未設定 | `~/.msstore-env` の4値を確認。Entra 認証テスト: Mac で `source ~/.msstore-env` 後 token 取得 |
| `makeappx` / `signtool` なし | Windows 10/11 SDK をインストール |
| `New-ApplicationSubmission failed` | Partner Center で初回手動提出が一度必要な場合あり。Store ID `9PKH91ZX0W9J` を確認 |
| Identity Version 第 4 桁が 0 でない | `version.txt` を上げ、`build.ps1 -Clean` で再ビルド |
| manifest 日本語文字化け | DisplayName に日本語を入れない（Partner Center の各言語 Product name を使う） |

---

## 参照

| ファイル | 内容 |
|----------|------|
| [store-metadata.md](store-metadata.md) | Partner Center 入力文言・Restricted capability 短縮版 |
| [../windows/scripts/store-cert-notes-en.txt](../windows/scripts/store-cert-notes-en.txt) | Notes for Certification 正本 |
| [windows/BUILD.md](../windows/BUILD.md) | ビルドコマンド・manifest 注意点 |
| [build-common/microsoft-store-setup.md](../../build-common/microsoft-store-setup.md) | 共通認証・MCP / msstore CLI |
| [warp-to-here/docs/store-submit.md](../../warp-to-here/docs/store-submit.md) | Entra 初回登録手順（スクリーンショット付きの元ネタ） |

Store URL: https://apps.microsoft.com/detail/9PKH91ZX0W9J
