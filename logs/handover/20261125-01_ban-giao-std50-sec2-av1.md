# PHIẾU BÀN GIAO 20261125-01 - STD50 PR 1, SEC2, AV1, SEC1 (phiên tsufiles-e9)

- **Ngày**: 25/11/2026 - **Phiên**: tsufiles-e9 (AKB `SES-20261125-0147-9323`)
- **PR**: #124 `feat/std5-ban-sac-files` (chuẩn v5.4.0)

## 1. Đã xong

| Phiếu | Kết quả |
| --- | --- |
| STD50 PR 1 | Website nạp token chuẩn + `data-product="files"`, bỏ bảng màu tự khai, bo góc `mem`, Lexend tự host, mặc định Hệ thống. Desktop: `Services/StandardTokens.cs` nhúng JSON chuẩn, `NightOwlPalette` đọc từ đó. Test `StandardTokensTests`. |
| SEC2 | `/security/` + `docs/security/trust.json` (E1, E2, E3, E5, E6, E10); security.txt mailto + cổng Expires. |
| AV1 | ĐÓNG: dmg arm64 quét lại 0/74; `docs/store/antivirus.md`; job `virustotal` trong `release.yml`. |
| SEC1 | ĐÓNG: bảng tên bí mật ở phiếu. |
| QR1 | STD-SYNC + UI1 desktop (test viên chỉ báo). |
| UI2 | Mục 0: preload font màn đầu; không có fade-in/scroll-reveal. |

## 2. Việc tiếp theo (theo thứ tự)

1. **STD50 PR 2**: bố cục `cong-cu` (vùng thao tác chính căn giữa màn đầu) + hoạ tiết `quy-dao` tĩnh ở hero (vẽ bằng
   `accent`/`glow`, ≤ 15%) + sinh lại `website/public/og-image.png`; ảnh trước/sau Sáng + Tối. Gộp với **UI2 phần A**
   (quay khung hình tải lạnh, 3 khổ 360/768/1366, console/mạng) và **UI2 phần B** (chờ bảng màu mới của tsudev-standards,
   không tự đổi hex); font dự phòng `size-adjust` đo thật (UI2 mục 0.2).
2. **SEC2 E8 SignPath**: kiểm điều kiện trên signpath.org, điền đơn qua Edge hồ sơ `tsudev-bp` (AKB
   `LES-20261122-2127-037b` của tsucontact), 👤 xác nhận email. Cập nhật `trust.json` + `docs/store/signpath.md`.
3. **PRO1/PRO2** (Pro 490.000 ₫/năm, nhắc gia hạn 7 ngày, 6 test bắt buộc) + **QR4** (đăng nhập 13.2): TsuFiles chưa có
   tài khoản; làm khi thư viện .NET `TsuSync` (PRO-D3) có. Phần làm được ngay: kiểm kê dữ liệu rời máy, ADR.
4. Đo lại trust.json trước 120 ngày (build dừng khi quá hạn); quét VT bản phát hành kế tiếp.

## 3. Việc của chủ dự án (👤)

- Đặt secret VirusTotal cho repo (agent bị bộ phân loại chặn): dùng `tsu-nhan-bi-mat --stdout | gh secret set
  VT_API_KEY -R tsudev-org/tsufiles` hoặc đặt qua giao diện GitHub.
- **Đổi khóa VirusTotal**: khóa trong `~/.config/tsudev/virustotal.env` đã lộ vào log phiên tsufiles-e9 (AKB
  `ISS-20261125-0200-8ae9`); tạo khóa mới, lưu đúng dạng `VT_API_KEY=...`.
- Deploy website sau khi gộp #124: `cd website && npm ci && npm run deploy` (nếu agent bị chặn).

## 4. Bài học

- Tệp bí mật dạng khóa thô: KHÔNG `source` (AKB `ISS-20261125-0200-8ae9`).
- Test xUnit tạo `SolidColorBrush`/`ResourceDictionary` PHẢI là `[AvaloniaFact]/[AvaloniaTheory]`: chạy trên luồng
  xUnit thường làm 10 test headless khác lỗi dọn dẹp "The calling thread cannot access this object".
