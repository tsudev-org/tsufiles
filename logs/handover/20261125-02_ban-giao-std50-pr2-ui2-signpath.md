# PHIẾU BÀN GIAO 20261125-02 - STD50 PR 2, UI2 phần A, SEC2 E8 chuẩn bị (phiên tsufiles-c4)

- **Ngày**: 25/11/2026 - **Phiên**: tsufiles-c4 (AKB `SES-20261125-0923-cecc`)
- **PR**: #126 `feat/std5-cong-cu-quy-dao` (gộp `391c97e9`), #127 `feat/sec2-code-signing-policy` (gộp `305347a2`)
- **Website**: deploy version `22a79ee0`; production rà 12 trang x 3 khổ x Sáng/Tối sạch, og-image khớp SHA-256 bản build

## 1. Đã xong

| Phiếu | Kết quả |
| --- | --- |
| STD50 | ĐÓNG. PR 2 (#126): trang chủ bố cục `cong-cu` (khối thao tác căn giữa: ví dụ chuyển đổi, nút Tải cho Windows + dung lượng, liên kết hệ khác, lệnh Scoop có nút Chép), hoạ tiết `quy-dao` tĩnh ở hero, og-image có vòng quỹ đạo (màu đọc từ `products/files.json`). Ảnh trước/sau: `docs/anh-chup/20261125-std50-pr2/`. App: màn đầu đã là vùng thả tệp lớn căn giữa - không đổi. |
| UI2 | Phần A xong (12 trang x 360/768/1366 x Sáng/Tối + quay khung hình tải lạnh): sửa tràn ngang `/ban-quyen/` khổ 360, sửa xung đột hai trình chép `[data-chep]` ở `theme.js`, font dự phòng Be Vietnam Pro đo thật (mục 0.2). Phần B: chờ bảng màu mới của tsudev-standards. Phiếu còn MỞ. |
| SEC2 E8 | Đo ngưỡng SignPath 1/6 ⇒ KHÔNG nộp. Đã chuẩn bị (#127): trang `/code-signing-policy/`, `tools/signpath/don.json` + `nop-don.mjs`. |

## 2. Việc tiếp theo (theo thứ tự)

1. **UI2 phần B** khi tsudev-standards phát hành bảng màu Sáng dịu (UX2-b/c của tsudev): sync `.standards/`, chụp lại
   3 khổ x Sáng/Tối, đóng phiếu UI2. Có thể đề xuất ở tsudev-standards thêm font dự phòng cho `--font-display`
   (Lexend 600 so Arial Bold: size-adjust 105,1%).
2. **PRO1/PRO2 + QR4**: chờ thư viện .NET `TsuSync` (PRO-D3). Làm được ngay: kiểm kê dữ liệu rời máy + ADR.
3. **SignPath**: đo lại ngưỡng mỗi tháng (`npm run he-sinh-thai:tin-hieu` ở repo tsudev); đạt >= 4/6 thì 👤 nộp.
4. Lặt vặt: chạy `tools/thuong-hieu/tao-tai-san.py` làm thay đổi `src/TsuFiles.App/Assets/Icons/tsufiles.ico` (không
   cố ý, đã hoàn lại) - xem có phải script ghi ICO không tất định không.

## 3. Việc của chủ dự án (👤)

- Còn từ phiếu 20261125-01: secret `VT_API_KEY`, **đổi khóa VirusTotal** đã lộ.
- SignPath (khi đạt >= 4/6): `cd website && node ../tools/signpath/nop-don.mjs --headed` (xem ảnh) rồi `--submit`,
  xác nhận email. Người nộp đọc từ `~/.config/tsudev/signpath-applicant.json`.

## 4. Bài học (AKB)

- `LES-20261125-0959-5522`: font dự phòng - `local()` cần tên đầy đủ/PostScript; đo bằng fontTools + Arimo; kiểm bằng
  fontconfig tạm + CDP `CSS.getPlatformFontsForNode`.
- `LES-20261125-0959-4d6f`: Pillow paste RGBA qua mặt nạ từ chính alpha ⇒ alpha nhân hai lần.
- Cổng build `/security/`: `trust.json` chỉ được liên kết tới bên thứ ba (không trỏ về chính website).
