# PHIẾU BÀN GIAO - CI main, ADR-0004 S2a/S2b, khảo sát phiên bản V1

- **Mã phiếu**: 20261117-02
- **Từ**: phiên tsufiles 17/11/2026 (tiếp nối phiếu `20261117-01`)
- **Trạng thái**: MỞ (việc tiếp theo ở mục 2)

## 1. Đã làm

| PR | Nội dung | Ghi chú |
| --- | --- | --- |
| #97 | CI `main` đỏ: `ShareServer` gọi `Received` TRƯỚC khi trả 200 (phiếu 01 mục 1b); `SetupWindowTests` chờ tiến độ bằng `PumpUntil` (máy Windows bận thấy 0% ở "fr") | Phiếu `20261117-01` mục 1b ĐÓNG khi gộp |
| #98 | S2a: `Core/Share/QrCode.cs` tự viết (byte, mức M, phiên bản 1-40), không thêm gói NuGet | 19 ca trùng từng ô với segno; `tools/qr/doi-chieu-qr.py` |
| #99 | S2b: trang "Chia sẻ với điện thoại" + `PhoneShareService` + `ShareServer.AddFiles` + 21 khóa x 22 ngôn ngữ | Chứa commit của #98 - gộp #98 TRƯỚC |
| #101 | P0 phiếu toàn hệ `20261117-P2`: đồng bộ bộ quy ước v3.3.0 -> v3.7.0 + khối "Phiên kế tiếp" đầu STATE | Phiếu `20261118-P0` ĐÓNG |
| #102 | S2c: mở trang chia sẻ khi thiết bị xin kết nối (cả khi ẩn ở khay), `AppSettings.ShareReceiveFolder`, `docs/vi/CHIA-SE-DIEN-THOAI.md` | Chứa commit của #99 - gộp #99 TRƯỚC |

- CodeQL: 3 cảnh báo `cs/cleartext-storage` (#24-#26, `Program.cs` log chẩn đoán) mở lại sau `2356154b` - đã đóng
  false positive như #9-#12.
- Phiếu toàn hệ `20261117-V1` (phiên bản thống nhất): đã đọc, ghi khảo sát updater vào mục 6 của bản sao. tsufiles là
  bước 5, CHỜ tsudev-standards v3.7.0 và bước 2-4.

Bài học:
- segno 1.6.6 thêm thừa byte `0x00` khi chuỗi bit đã tròn byte (trái ISO 18004 mục 7.4.10) - đối chiếu QR phải vá
  hàm `write_padding_bits` (script đã làm). OpenCV (`tools/ung-ho/requirements.txt`) giải mã được mã QR trên ảnh
  chụp màn hình của test.
- Trang cài đặt được dựng lại mỗi lần chuyển trang (`MainWindowViewModel.CreatePage`): trạng thái sống lâu phải ở
  dịch vụ singleton (`AppHost`).
- Thêm khóa i18n phải thêm cho ĐỦ 22 tệp (`LocalizationTests.Every_language_has_exactly_the_english_keys`).

## 2. Việc tiếp theo (mỗi mục một phiên/PR)

1. Đầu phiên: kiểm #99, #101, #102 đã gộp chưa (`gh pr list -R tsudev-org/tsufiles`). Chưa thì gộp theo thứ tự
   #99 -> #101 -> #102 khi CI xanh (#97, #98 đã gộp 18/11; #100 là phiếu này). Sau đó xem CI `main` xanh.
2. **S2d** - phần S2 còn lại: (a) Playwright WebKit cho trang điện thoại (`npx playwright install webkit` + thư viện
   hệ thống, có thể cần chủ dự án chạy `sudo npx playwright install-deps webkit`); (b) mục CHANGELOG cho bản phát
   hành tới (dòng `vi:`/`en:` trong khối `<!-- update -->`, mức `optional`). (Xong ở #102: báo khi ẩn ở khay, nhớ thư
   mục nhận, hướng dẫn + tường lửa.)
3. **S3** - tìm máy TsuFiles khác qua UDP multicast; khảo sát LocalSend (iOS tối thiểu, giấy phép đặc tả).
4. **S4** - app iOS gốc: CHỜ chủ dự án (Mac + Apple Developer 99 USD/năm).
5. **V1 bước 5** (sau khi tsudev-standards v3.7.0 và các bước 2-4 xong): đổi `2.4.x-vn.N` -> `26.M.DDNN`; BẮT BUỘC
   đính kèm thêm bộ tệp tên cũ `tsudev-files-<ver>-win-Setup.exe` + `-SHA256SUMS.txt` (+ chữ ký) cho updater
   2.4.0-vn.1 (xem bản sao phiếu V1 mục 6); đổi `packaging/package.json` `tagRegex`.

## 3. Ghi chú môi trường

- Chạy công cụ QR: `python3 -m venv <thư mục> && <thư mục>/bin/pip install -r tools/ung-ho/requirements.txt`.
- `find /` trên WSL quét cả ổ Windows - rất chậm; ảnh test ở `tests/TsuFiles.App.Tests/bin/Debug/net10.0/screenshots/`.
- `SpreadsheetPrintStudioTests.Translated_print_settings_fit_at_the_minimum_window_width(pt-BR)` trượt trên WSL cả ở
  `main` (môi trường/font); CI xanh - không phải lỗi do S2.
