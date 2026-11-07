#!/bin/sh
# Chạy bản thử giao diện tại http://127.0.0.1:5090 (SQLite in-memory, không đụng DB Compose).
# Mã nguồn web gắn chỉ đọc; ảnh bìa tải lên ghi vào thư mục tạm riêng.
set -eu
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
UPLOADS=${PREVIEW_UPLOADS:-/tmp/bookstore-preview-uploads}
mkdir -p "$UPLOADS" "$ROOT/src/BookStore.Web/wwwroot/uploads"
chmod 1777 "$UPLOADS"  # sticky bit: container (uid khác) ghi được, không xóa tệp của người khác
docker compose -f "$ROOT/compose.yaml" --profile test build test
docker rm -f bookstore-ui-preview >/dev/null 2>&1 || true
docker run -d --rm --name bookstore-ui-preview -p 127.0.0.1:5090:5090 \
  -v "$ROOT/tools/preview:/preview:ro" \
  -v "$ROOT/src/BookStore.Web:/preview-web:ro" \
  -v "$UPLOADS:/preview-web/wwwroot/uploads" \
  -e ASPNETCORE_TEST_CONTENTROOT_BOOKSTORE_WEB=/preview-web \
  --entrypoint dotnet bookstore-btl-test:local \
  run --project /preview/Preview.csproj --artifacts-path /tmp/preview-artifacts
i=0
until curl -s -o /dev/null http://127.0.0.1:5090/; do
  i=$((i + 1)); [ "$i" -gt 60 ] && { echo "PREVIEW_START_TIMEOUT: xem docker logs bookstore-ui-preview"; exit 1; }
  sleep 3
done
echo "Preview: http://127.0.0.1:5090 (reader / manager)"
