# 詐欺ブロック（Windows）ビルド手順

作業ディレクトリは常に **`windows` フォルダ**（この README と同じ階層）を起点にする。

## 前提

- **.NET 8 SDK**（`dotnet --version` で確認）
- **MSIX を作る場合**: Windows 10/11 SDK（`makeappx.exe` が `C:\Program Files (x86)\Windows Kits\10\bin\...\x64` 付近に存在すること）

## バージョンの正

- **`version.txt`** の値がソース。`SagiBlock.csproj` の `<Version>` と MSIX manifest の Version（第 4 桁 0）をビルド時に合わせる。

## いつも使うコマンド

| 目的 | コマンド |
|------|----------|
| 通常のフルビルド（EXE x64/arm64 + MSIX） | `.\build.ps1` |
| `publish` を消してからフルビルド | `.\build.ps1 -Clean` |
| EXE のみ（MSIX なし） | `.\build.ps1 -Exe` |
| Store 用アイコン素材の再生成 | `.\generate-assets.ps1` |

PowerShell の実行ポリシーで止まる場合は、必要に応じて `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` などで許可する。

## 成果物の場所

- **ポータブル EXE**（自己完結）: `publish\exe\x64\` と `publish\exe\arm64\`
- **MSIX / バンドル**: `publish\msix\`（`SagiBlock.msixbundle` — **Partner Center アップロード用**、各 `SagiBlock_x64.msix` / `SagiBlock_arm64.msix`）

Store 用タイル PNG が無い場合、`generate-assets.ps1` が `SagiBlock\Assets\app-icon.png` から自動生成する。

`run-build.cmd` は FaviconApplier と同様に `last-build.log` を書く。

## Microsoft Store 申請

パッケージの実体は **`publish\msix\SagiBlock.msixbundle`**。

- **申請用文言・Product identity・Notes for Certification**: リポジトリの [`docs/store-metadata.md`](../docs/store-metadata.md)
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
