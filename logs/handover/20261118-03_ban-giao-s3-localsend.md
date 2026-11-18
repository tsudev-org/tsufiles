# PHIẾU BÀN GIAO - S2d gộp, S3 LocalSend (ADR-0004), khóa hợp đồng phiên bản V1, đóng AN1

- **Mã phiếu**: 20261118-03
- **Từ**: phiên tsufiles-3c, 18/11/2026 (tiếp nối phiếu `20261117-02`)
- **Trạng thái**: MỞ (việc tiếp theo ở mục 2)

## 1. Đã làm

| PR | Nội dung | Ghi chú |
| --- | --- | --- |
| #104 (gộp) | S2d: Playwright WebKit (iPhone 8) cho trang điện thoại, CHANGELOG bản tới | Phiên trước mở, phiên này gộp khi CI xanh |
| #105 (gộp) | S3a: TsuFiles nói giao thức LocalSend v2 - multicast `224.0.0.167:53317`, `/api/localsend/v2/*`, `LocalSendClient` (HTTPS chỉ tin vân tay đã công bố), lệnh `tsufiles send` | Mã do phiên trước viết dở (chưa commit); phiên này rà, gia cố, thử thật |
| #106 (gộp) | S3b: mục "Thiết bị gần đây (LocalSend)" + nút **Gửi tệp** ở trang chia sẻ, 11 khóa x 22 ngôn ngữ | Chứa commit của #105 - gộp #105 TRƯỚC |
| #107 (gộp) | Test khóa hợp đồng phiếu V1: bản 2.4.x nhận `v26.M.DDNN` là bản mới | V1 bước 5 vẫn CHỜ bước 2-4 (mục 2) |

Gia cố trong #105 (tìm khi rà/thử thật):
- JSON từ máy lạ có `null` ở trường bắt buộc (`alias`, `info`, phần tử `files`) gây NullReferenceException ->
  `RespectNullableAnnotations` + chặn tay phần tử từ điển null => 400.
- Mỗi gói multicast announce kích một yêu cầu HTTP `/register` ra ngoài -> trần 4 lần trả lời cùng lúc.
- `tsufiles send --to ""` khớp MỌI máy (`Contains("")`) và gửi luôn -> rỗng không khớp máy nào.
- Hai TsuFiles trên một máy (cùng IP) không chọn được bằng `--to <IP>` -> thêm `--to <IP>:<cổng>`, danh sách mơ hồ
  in kèm `--to IP:cổng`.

Thử thật trên WSL: `tsufiles share` và `tsufiles send` là hai tiến trình riêng, tìm nhau qua multicast thật, tệp tên
tiếng Việt + tệp 3 MB khớp từng byte; `.bin` bị bỏ đúng thiết kế (đuôi chạy được) và CLI báo "chỉ nhận một phần".

Khác:
- Phiếu AN1 (bản sao `20261118-AN1_*`) ĐÓNG: #103 đã gộp; ghi dòng tsufiles ở phiếu gốc `~/projects/logs/handover/20261118-AN1_*`.
- files.tsudev.com: quét 3 trang x 2 cỡ x 2 chủ đề - 12/12 OK. Không có thay đổi nội dung website từ lần deploy trước
  (#103 chỉ chuyển gói sang devDependencies) => không deploy.

Bài học:
- `dotnet format --include <đường dẫn>` cục bộ trả 0 nhưng CI vẫn đỏ WHITESPACE (khối `{ }` dưới nhãn `case`). Luôn chạy
  đúng lệnh CI: `dotnet format TsuFiles.slnx --verify-no-changes --severity warn` (khoảng 3 phút) trước khi đẩy.
- Thêm khóa i18n: CHÈN văn bản sau khóa cuối, KHÔNG `json.load` rồi `json.dump` cả tệp (làm xáo định dạng cũ, diff 2000 dòng).
- `tsufiles share` không tự dừng khi stdin đóng (chỉ Ctrl+C / 15 phút) - thử bằng `script` thì phải tự dừng tiến trình.

## 2. Việc tiếp theo (mỗi mục một phiên/PR)

1. (XONG trong phiên) #105, #107, #106 đã gộp 18/11/2026 khi CI xanh (`main` = `3f7be408`). Đầu phiên tới: xem CI `main` xanh.
2. **S3c (tùy chọn)** - thử với app LocalSend thật trên điện thoại (cần chủ dự án cầm máy): iPhone 8 mở LocalSend, thấy
   `TsuFiles (<tên máy>)`, gửi ảnh; TsuFiles gửi tới iPhone (LocalSend iOS mặc định HTTPS - kiểm vân tay chứng thư).
   Chưa hỗ trợ: PIN (giao thức `?pin=`), chế độ HTTPS phía nhận - ghi ở ADR-0004 mục 4.
3. **S4** - app iOS gốc: CHỜ chủ dự án (Mac + Apple Developer 99 USD/năm).
4. **V1 bước 5** - đổi `2.4.x-vn.N` -> `26.M.DDNN`: CHỜ bước 2-4 (đo 18/11: tsucwico `tsudev-cwico-v26.11.10.2`,
   tsuswico `v26.8.18.2`, trafficmonitor `V26.11.1202-vn` - chưa repo nào theo khuôn mới). Khi làm: test #107 phải còn
   xanh; BẮT BUỘC đính kèm bộ tệp tên cũ `tsudev-files-<ver>-win-Setup.exe` + `-SHA256SUMS.txt` (+ `.sig`) và bản Linux
   cho updater 2.4.0-vn.1 (phiếu V1 mục 6); đổi `packaging/package.json` `tagRegex`.
5. P2 (MSIX), P4 (ký), P5 (notarize macOS) của phiếu `20261117-P2` hàng 4: chờ việc 👤 (Partner Center, chứng thư, Apple).

## 3. Ghi chú môi trường

- Thử LocalSend hai tiến trình: `(sleep 8; echo y; sleep 10) | timeout 20 script -qfc "dotnet <cli.dll> share -o <thư mục>" <log>`
  ở nền, rồi `dotnet <cli.dll> send <tệp> --to <IP:cổng>`. CLI build ở `src/TsuFiles.Cli/bin/Debug/net10.0/tsufiles-cli.dll`.
- Multicast thật chạy được trên WSL (địa chỉ `172.28.x.x`).
