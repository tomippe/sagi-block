# 詐欺ブロック（Windows）ビルド手順

作業ディレクトリは常に **`windows` フォルダ**（この README と同じ階層）を起点にする。

## 前提

- **.NET 8 SDK**（`dotnet --version` で確認）
- **MSIX を作る場合**: Windows 10/11 SDK（`makeappx.exe` / `signtool.exe` が `C:\Program Files (x86)\Windows Kits\10\bin\...\x64` 付近に存在すること）
- **Store 提出用認証**: `%USERPROFILE%\.msstore-env`（Mac と同内容。`build-common/msstore-env.example` 参照）
  - Entra: `MSSTORE_*` または `SB_*`（POUCHES Store Collections の Client ID / Secret）
  - 署名: `MS_STORE_SIGNING_PFX` / `MS_STORE_SIGNING_PFX_PASSWORD`
  - PFX 未設定時は `..\pouches\native\windows\signing\StudioTomippe-MSIX.pfx` を自動参照
- **プロジェクト `.env`**: `MS_STORE_PRODUCT_ID=9PKH91ZX0W9J`（Store 提出時）

## バージョンの正

- **`version.txt`** の値がソース。`SagiBlock.csproj` の `<Version>` と MSIX manifest の Version（第 4 桁 0）をビルド時に合わせる。

## いつも使うコマンド

| 目的 | コマンド |
|------|----------|
| 通常のフルビルド（EXE x64/arm64 + 署名済み MSIX） | `.\build.ps1` |
| `build` を消してからフルビルド | `.\build.ps1 -Clean` |
| EXE のみ（MSIX なし） | `.\build.ps1 -Exe` |
| ビルド後 Partner Center へ提出 | `.\build.ps1 -Publish` |
| 提出のみ（ビルド済み bundle がある場合） | `.\scripts\store-submit.ps1` |
| Store 用アイコン素材の再生成 | `.\generate-assets.ps1` |

PowerShell の実行ポリシーで止まる場合は、必要に応じて `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` などで許可する。

## 成果物の場所

- **ポータブル EXE**（自己完結）: `build\exe\x64\` と `build\exe\arm64\`
- **MSIX / バンドル**: `build\msix\`（`SagiBlock.msixbundle` — **Partner Center アップロード用**、各 `SagiBlock_x64.msix` / `SagiBlock_arm64.msix`。**build.ps1 が Partner Center 証明書で署名**）

Store 用タイル PNG が無い場合、`generate-assets.ps1` が `SagiBlock\Assets\app-icon.png` から自動生成する。

`run-build.cmd` は FaviconApplier と同様に `last-build.log` を書く。

### `%USERPROFILE%\.msstore-env`（Windows PC）

Mac の `~/.msstore-env` と同じ Entra / 署名情報を置く。PFX パスだけ Windows 向けに変更する例:

```
MS_STORE_SIGNING_PFX=Y:\Cursor\pouches\native\windows\signing\StudioTomippe-MSIX.pfx
MS_STORE_SIGNING_PFX_PASSWORD=（msstore-env と同じ）
MSSTORE_TENANT_ID=...
MSSTORE_SELLER_ID=...
MSSTORE_CLIENT_ID=...
MSSTORE_CLIENT_SECRET=...
```

`export` 付き（Mac 形式）でも読み込める。

## Microsoft Store 申請

パッケージの実体は **`build\msix\SagiBlock.msixbundle`**。

- **提出手順（作業指示書）**: [`docs/store-submit.md`](../docs/store-submit.md)
- **申請用文言・Product identity**: [`docs/store-metadata.md`](../docs/store-metadata.md)
- **Notes for Certification 正本**: [`scripts/store-cert-notes-en.txt`](scripts/store-cert-notes-en.txt)
- **apps.tomippe.jp 紹介ページ**: [`docs/app-page.md`](../docs/app-page.md)
- **動作・判定基準**: [`docs/behavior.md`](../docs/behavior.md)

manifest の DisplayName は **`SagiBlock`**（ASCII 予約名）。日本語 `詐欺ブロック` を manifest に入れると MSIX 化時に Partner Center で文字化けすることがある。

## トラブル時

- **MSIX フェーズで `makeappx` が見つからない**: Windows SDK をインストールする。
- **Invalid package identity / DisplayName**: [`docs/store-metadata.md`](../docs/store-metadata.md) の Product identity と manifest を照合する。
- **コンパイルエラー**: ビルドログの CS 番号を手掛かりに修正する。

## 過去にハマった点（manifest・エンコーディング）

`build.ps1` が manifest を読み書きするとき:

- **読み込み**: `Get-Content ... -Encoding UTF8`（省略すると日本語環境で文字化け）
- **書き込み**: UTF-8 BOM なし（`[System.IO.File]::WriteAllText` + `UTF8Encoding $false`）
- **DisplayName**: Partner Center 予約名 **`SagiBlock`** のみ。`SAGI BLOCK` / `Sagi Block` / 日本語は manifest に入れない

PowerShell の `-replace` で `$1` を使うときはバッククォート `` `$1 `` でエスケープする（FaviconApplier の BUILD.md 参照）。
