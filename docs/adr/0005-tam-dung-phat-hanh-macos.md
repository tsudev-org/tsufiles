# ADR-0005: Tạm dừng phát hành macOS đến quý 1/2027

- **Trạng thái**: Chấp nhận - chủ dự án quyết định 26/11/2026.
- **Phạm vi**: CHỈ macOS. Windows và Linux (x64, ARM64) vẫn build, kiểm thử và phát hành song song.

## 1. Bối cảnh

- Mỗi lần đẩy lên PR, CI chạy khoảng 80 phút, phần lớn do hai job `macos-15` và `macos-15-intel` (riêng test giao diện
  trên Mac Intel 50 phút trở lên). Runner macOS ít, hay phải chờ và đôi khi không được cấp.
- Chưa có máy Mac và chưa có Apple Developer Program: bản macOS không công chứng được, mới ở mức xem trước, không ai
  thử được trên máy thật.
- Ưu tiên hiện tại: Windows 10/11 cho văn phòng, cơ quan.

## 2. Quyết định

1. `portable.yml` chỉ build Linux; bỏ phần dò `macos` trong `ci.yml`. `magick-native-macos.yml` giữ lại (chỉ chạy tay).
2. Mã `src/TsuFiles.Platform.MacOS` và các nhánh `osx-*` trong script **giữ nguyên**. Dự án vẫn nằm trong
   `TsuFiles.slnx` nên được biên dịch trên Windows/Linux; không thêm job kiểm riêng.
3. Bản phát hành kế tiếp **không có** tệp `osx-*`. Tệp macOS của `v2.4.1-vn.1` **giữ trên GitHub Releases**.
4. `packaging/package.json` bỏ asset macOS và kênh `brew`; cask Homebrew đóng băng ở 2.4.1-vn.1.
5. README, trang Tải về và ghi chú phát hành nói rõ macOS tạm dừng.

## 3. Ngoại lệ với quy ước chung

`PRODUCT_POLICY.md` mục 2 yêu cầu bản macOS được "phát triển và kiểm thử đầy đủ ngay bây giờ". ADR này là ngoại lệ có
thời hạn cho riêng TsuFiles, đến khi đạt điều kiện ở mục 5.

## 4. Hệ quả

- Người đã cài macOS 2.4.1 không nhận bản mới. Thẻ cập nhật của bản đó có thể vẫn báo có phiên bản mới; trang phát hành
  của bản đó không có tệp cho macOS (đã ghi trong ghi chú phát hành).
- `website/public/phien-ban.json`: nâng `toiThieu` trên **2** (số build của 2.4.1) sẽ khóa bản macOS mà không có bản
  để cập nhật. Việc nâng đó cần chủ dự án duyệt riêng điểm này ([CAP-NHAT-BAT-BUOC.md](../vi/CAP-NHAT-BAT-BUOC.md)).
- Lỗi chỉ xuất hiện khi chạy trên macOS sẽ không được phát hiện trong thời gian tạm dừng.

## 5. Điều kiện bật lại

Có máy Mac **và** tài khoản Apple Developer Program (dự kiến quý 1/2027). Khi đó:

1. Thêm lại hai mục `macos-15`/`osx-arm64`, `macos-15-intel`/`osx-x64` vào matrix của `portable.yml`, nâng
   `timeout-minutes` về 180; cân nhắc khôi phục phần dò `macos` trong `ci.yml` (lịch sử git của ADR này).
2. Khôi phục asset macOS và kênh `brew` trong `packaging/package.json`; mở lại cask ở repo `homebrew-tap`.
3. Chạy test và thử trên máy thật, công chứng (notarize) theo `DISTRIBUTION_TRUST.md` mục 1.2.
4. Sửa README, website, đánh dấu ADR này "Thay thế".
