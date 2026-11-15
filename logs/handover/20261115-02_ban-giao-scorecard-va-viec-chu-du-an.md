# PHIẾU BÀN GIAO - Scorecard đạt >= 7, ghim rustup-init; còn lại là việc của chủ dự án

- **Mã phiếu**: 20261115-02 (số 02 để khỏi trùng phiếu toàn hệ `~/projects/logs/handover/20261115-01`)
- **Từ**: phiên agent-files (15/11/2026) - **Đến**: phiên kế tiếp của repo này
- **Trạng thái**: ĐÓNG 16/11/2026 (2.5, 2.7 xong ở #86, #85; việc còn lại chép sang phiếu `20261116-01`)
- **Phiếu trước**: `20261114-03` (ĐÓNG, việc còn mở chép sang mục 2 dưới đây). Phiếu toàn hệ: `20261115-01` (dòng
  tsufiles đã đánh dấu xong).

## 0. Đọc trước

- Repo `tsudev-org/tsufiles`; `gh` luôn kèm `-R tsudev-org/tsufiles`. KHÔNG tạo lại tên `tsudev-files` (ADR-0003 mục 7).
- Quyền commit/push/PR/merge/`wrangler deploy` phải được chủ dự án nói lại trong chat mỗi phiên.
- Đo Scorecard: `curl -s https://api.securityscorecards.dev/projects/github.com/tsudev-org/tsufiles` (API công khai
  không trả chi tiết từng dòng bị bắt).

## 1. Đã xong trong phiên

- Scorecard đo lúc 12:25 UTC 15/11/2026 ở commit `3467de31`: **7.1/10** (đạt chỉ tiêu >= 7 của phiếu toàn hệ).
  Theo hạng mục: Code-Review 0, Maintained 0, Fuzzing 0, Branch-Protection 3, CII-Best-Practices 5 (passing),
  Pinned-Dependencies 8, Signed-Releases 8, các mục còn lại 10, Packaging -1.
- PR #83 `ci/ghim-rustup-init`: bỏ `curl https://sh.rustup.rs | sh` ở `portable.yml`; tải `rustup-init` 1.29.3 từ
  `static.rust-lang.org/rustup/archive` và kiểm SHA-256 (linux x64/arm64; Windows ARM64 ở `windows-arm64-native.yml`
  chuyển từ `rustup/dist` trôi sang bản ghim + `Get-FileHash`). Băm từ tệp `.sha256` chính thức khớp băm tự tính 3/3.
  Thử thật đường Linux x64 trên WSL: khớp băm, `rustup 1.29.3` chạy. Runner GitHub có sẵn rustup nên CI không đi qua
  nhánh này. Kết quả CI/gộp: xem mục 3.

## 2. Việc tiếp theo (👤 = chỉ chủ dự án)

- [ ] 2.1. 👤 Secret `WINGET_TOKEN` (PAT `public_repo`) - chưa có (kiểm `gh secret list` 15/11/2026). Có rồi thì agent
  nộp winget qua komac và chuyển `winget` từ `planned` sang `channels` trong `packaging/package.json`.
- [ ] 2.2. 👤 Cloudflare: tắt Security > Bots > JavaScript Detections cho `files.tsudev.com` (lỗi CSP ở console). Lưu ý
  STATE mục Quyết định 10/11/2026 từng chốt KHÔNG tắt (thiết lập cấp zone) - chủ dự án chọn lại.
- [ ] 2.3. 👤 #42 (repo `tsudev-org/tsufiles-assets` cho tệp build) và #44 (khóa ký dự phòng).
- [ ] 2.4. 👤 SignPath (D5) - chờ đủ ngưỡng (kế hoạch `~/projects/logs/handover/20261113-02` mục 5).
- [ ] 2.5. 👤 Quyết định bật `RestorePackagesWithLockFile` (+ `dotnet restore --locked-mode` trong CI): phần còn lại của
  Pinned-Dependencies; mọi PR Dependabot NuGet sẽ phải cập nhật `packages.lock.json`. Đồng ý thì agent làm.
- [ ] 2.6. 👤 Branch-Protection/Code-Review (3 và 0): ruleset bắt buộc 1 người duyệt, áp cho admin. Chỉ có một người
  bảo trì nên sẽ chặn chính mình - chỉ bật khi có người duyệt thứ hai.
- [ ] 2.7. Agent: ở bản phát hành tới, đính kèm bundle chứng thực (`*.intoto.jsonl`) vào Release để Signed-Releases
  lên 10 (`release.yml`, ghi vào `docs/vi/PHAT-HANH.md`). Không đổi tên tệp hiện có (trình cập nhật đọc chúng).
- Không cần làm: Maintained tự lên khi repo đủ 90 ngày; Fuzzing - Scorecard không nhận fuzz xUnit của .NET.

## 3. Cổng kiểm và kết quả

- `scripts/check-standards.sh` ĐẠT (3 lưu ý cũ); YAML hai workflow hợp lệ.
- PR #83: CI 21/21 xanh, gộp 16/11/2026 (`04e420da`). Điểm Scorecard sau gộp chưa đo (Scorecard chạy theo lịch) - phiên sau đo lại.
