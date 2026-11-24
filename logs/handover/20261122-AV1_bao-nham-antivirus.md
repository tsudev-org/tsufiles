# PHIẾU YÊU CẦU AV1 - Antivirus / báo nhầm (tsufiles)

- **Mã phiếu**: 20261122-AV1
- **Thời điểm**: 22/11/2026
- **Người gửi**: agent-tsuswico (phiên tsuswico-6e) theo uỷ quyền của chủ dự án
- **Phiếu điều phối**: `~/projects/logs/handover/20261122-AV1_bao-nham-antivirus-toan-he.md` (bảng kết quả cả hệ + thư soạn sẵn từng hãng)
- **Chuẩn**: `.standards/docs/DISTRIBUTION_TRUST.md` mục 3, 3.1
- **Trạng thái**: ĐÓNG (25/11/2026)

## 1. Kết quả VirusTotal 22/11/2026 - bản Latest `v2.4.1-vn.1`

| Tệp | Kết quả | Engine |
| --- | --- | --- |
| `tsudev-files-2.4.1-vn.1-osx-arm64.dmg` | 1+0/74 | Microsoft `Trojan:Script/Wacatac.C!ml` |
| `tsudev-files-2.4.1-vn.1-osx-x64.dmg` | 0+0/74 | - |
| `tsudev-files-2.4.1-vn.1-win-Portable.zip` | 0+0/74 | - |
| `tsudev-files-2.4.1-vn.1-win-Setup.exe` | 0+0/74 | - |

## 2. Việc cần làm

1. Tạo/cập nhật `docs/store/antivirus.md` (mẫu `.standards/docs/templates/TRUST_TICKET.md` mục D): tệp, SHA-256, engine, ngày.
2. Kiểm phần của repo này trong thư soạn sẵn (phiếu điều phối mục 3): mô tả sản phẩm đúng hành vi thật, link Release đúng.
3. 👤 Chủ dự án gửi thư/biểu mẫu (agent không có hộp thư/tài khoản). Ghi ngày gửi + mã hồ sơ vào sổ.
4. Đo lại VT 3-7 ngày sau (`~/.config/tsudev/virustotal.env`, API `/api/v3/files/<sha256>`); hết cờ ⇒ ghi ngày gỡ.
5. Bản phát hành sau: Release nháp → job VirusTotal → sạch mới công bố; cờ ⇒ quét riêng exe/zip + bản trước làm đối chứng trước khi đổi đóng gói (bài học tsuswico ISS-20261122-2153-055f).
- Microsoft `Trojan:Script/Wacatac.C!ml` (học máy) với dmg macOS arm64: 👤 nộp WDSI (Software developer). Repo CHƯA có job VirusTotal ⇒ thêm workflow dùng chung.

## 3. Khép phiếu

Đổi Trạng thái → ĐÓNG khi mọi cờ đã gỡ (hoặc 👤 chấp nhận) và sổ đã ghi.

## 4. Kết quả xử lý

- 25/11/2026 - tsufiles-e9: yêu cầu VirusTotal quét lại dmg arm64 ⇒ **0/74** (cờ Microsoft `Wacatac.C!ml` đã tự gỡ,
  không cần nộp WDSI). Sổ `docs/store/antivirus.md`; sửa mô tả sản phẩm sai trong thư soạn sẵn ("file manager" ⇒
  "offline file converter"); thêm job `virustotal` (workflow dùng chung, ghim SHA) vào `release.yml`. Còn 👤: đặt
  secret `VT_API_KEY` cho repo (agent bị chặn). Phiếu ĐÓNG.
