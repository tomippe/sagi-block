#!/bin/bash
set -e

# ===== 詐欺ブロック（SagiBlock）リリース同期スクリプト =====
# Windows 版のコンパイルは Windows PC 上の windows/build.ps1 で行う。
# 本スクリプトは manifest 同期・FTP・版上げ・Git コミットを担当する。

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

APP_NAME="sagi-block"
VERSION_FILE="windows/version.txt"
DIST_DIR="../apps.tomippe.jp/sagi-block"

source "$SCRIPT_DIR/../build-common/version.sh"
source "$SCRIPT_DIR/../build-common/ftp-upload.sh"
source "$SCRIPT_DIR/../build-common/git-commit.sh"

COMMIT_MSG=""
NO_VERUP=false
while [ $# -gt 0 ]; do
    case "$1" in
        -cm) shift; COMMIT_MSG="$1" ;;
        -noverup) NO_VERUP=true ;;
    esac
    shift || true
done

VERSION=$(version_read "$VERSION_FILE")
echo "🚀 ${APP_NAME} v${VERSION} — リリース同期..."

mkdir -p "$DIST_DIR"

python3 -c "
import json, os
path = '$DIST_DIR/manifest.json'
data = {}
if os.path.exists(path):
    with open(path) as f: data = json.load(f)
data['name'] = 'SagiBlock'
data['version'] = '$VERSION'
data['win_version'] = '$VERSION'
with open(path, 'w') as f: json.dump(data, f)
"

echo "📤 manifest.json を FTP アップロード中..."
ftp_upload_file "$DIST_DIR/manifest.json" "sagi-block/manifest.json"

if ! $NO_VERUP; then
    echo ""
    echo "📝 次回用バージョンを更新しています..."
    version_save_next "$VERSION" "$VERSION_FILE"
fi

git_commit_build "$VERSION" "$COMMIT_MSG"

echo ""
echo "✅ ${APP_NAME} v${VERSION} — 同期完了"
echo "  manifest: $DIST_DIR/manifest.json"
echo "  Windows ビルド: cd windows && powershell -ExecutionPolicy Bypass -File .\\build.ps1"
