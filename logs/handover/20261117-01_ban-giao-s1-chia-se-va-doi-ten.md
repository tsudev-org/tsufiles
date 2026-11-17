# PHIẾU BÀN GIAO - Bỏ tên cũ, trang Ủng hộ, ADR-0004 S1 chia sẻ với điện thoại

- **Mã phiếu**: 20261117-01
- **Từ**: phiên tsufiles 16-17/11/2026 (tiếp nối phiên bị tắt đột ngột 16/11)
- **Trạng thái**: MỞ (việc tiếp theo ở mục 2; #90-#94 đã gộp, website deploy `db06ecac`)

## 1. Đã làm

| PR | Nội dung | Ghi chú |
| --- | --- | --- |
| #90 | PDF -> DOCX giữ chữ ký số/con dấu/bảng không viền; icon taskbar (ICO có lớp BMP); bỏ nút GitHub/Ủng hộ; mặc định đóng cửa sổ = khay (schema 75) | Đã gộp `3c95f0b3` |
| #91 | Trang `/ung-ho/` cùng khuôn tsudev.com, Chivo Mono (số 0 trơn), logo chung tsudev ở chân trang; bỏ tên cũ ở chữ tiếp thị website; sửa thông tin sai ở `/ban-quyen/` | Đã gộp + deploy (Worker version `70051174`). Phiếu `20261116-55` ĐÓNG; GIỮ tím riêng của TsuFiles |
| #92 | `tools/xoa-ten-cu.py`: bỏ chữ "Filee" ở chữ hiển thị/chú thích/tài liệu/metadata tệp xuất; `skills/tsufiles` | ADR-0003 mục 8 |
| #93 | ADR-0004 (kế hoạch S1-S4) + STATE | |
| #94 | S1: `Core/Share/` + `tsufiles share` | 20 test; chạy thử giả lập iPhone 8 đạt |

Bài học #92 (đã sửa trong PR): đổi chữ hàng loạt KHÔNG được chạm literal là định danh (thư mục cài cũ, Id gói,
khóa font `fonts:Filee#`, launcher Linux) và KHÔNG chạm `tests/` (dữ liệu di trú, Brand(), tên tệp phát hành cố ý
dùng tên cũ). Câu lịch sử ("Filee 2.1.0", "\"Filee\" -> ...") phải giữ. Script đã bảo vệ các mẫu này; `--kiem` = 0.
Dòng đã sửa phải bỏ em-dash (mục 7.1) - cổng "Quy ước tsudev" bắt.

## 2. Việc tiếp theo (mỗi mục một phiên/PR)

1. (XONG 17/11) Gộp #92/#93/#94 và deploy website (`db06ecac`). Đầu phiên sau: xem CodeQL của `main` sau
   `2356154b`; nếu 3 cảnh báo `cs/cleartext-storage` ở `Program.cs` (log chẩn đoán khởi động) mở lại thì đóng là
   false positive như các bản trước.
1b. **ƯU TIÊN - CI `main` đỏ** (run 43987129931, `778ee641`, job linux-arm64): test
   `ShareServerTests.An_upload_lands_under_a_safe_name_and_never_overwrites` chập chờn (Expected 2, Actual 1).
   Nguyên nhân: `ShareServer.ReceiveAsync` ghi phản hồi 200 TRƯỚC khi gọi `Received?.Invoke(...)`, nên client nhận
   phản hồi và test đếm trước khi sự kiện lần 2 chạy. Sửa: gọi `Received?.Invoke` trước `WriteTextAsync(stream, 200, ...)`
   (sự kiện chạy xong rồi mới trả lời). PR riêng, chạy lại CI.
2. **ADR-0004 S2** - nút "Chia sẻ với điện thoại" trong app desktop: trang/hộp thoại hiện mã QR từ
   `ShareServer.Links()`, hộp thoại duyệt thiết bị (`ShareOptions.Approve`), danh sách tệp nhận (`Received`),
   kéo thả tệp vào để chia sẻ. Cần bộ sinh mã QR: tự viết (chế độ byte, sửa lỗi M) hoặc gói MIT (QRCoder) - nếu thêm
   gói phải chạy `tools/cap-nhat-lock-nuget.sh`. Khóa i18n thêm vào `en.json` + `vi.json` (kiểm `tools/check_i18n_vi.py`).
   Thêm thử nghiệm Playwright WebKit (cần `npx playwright install webkit` + thư viện hệ thống) để kiểm trên engine Safari.
3. **S3** - tìm máy TsuFiles khác qua UDP multicast; khảo sát tương thích LocalSend (kiểm iOS tối thiểu của app
   LocalSend và giấy phép đặc tả trước khi làm).
4. **S4** - app iOS gốc: CHỜ chủ dự án quyết (Mac + Apple Developer 99 USD/năm). Nếu không, S2 là bản iOS chính thức.

## 3. Ghi chú môi trường

- `pkill -f`/`pgrep -f` với mẫu nằm trong chính lệnh Bash sẽ giết luôn shell của công cụ (thoát 144): viết script
  ra tệp và ghép mẫu từ nhiều mảnh.
- Playwright trên máy chỉ có Chromium; giả lập `devices['iPhone 8']` kiểm luồng trang, chưa kiểm engine WebKit.
