# PHIẾU BÀN GIAO - Lock file NuGet, đính kèm bundle chứng thực vào Release

- **Mã phiếu**: 20261116-01
- **Từ**: phiên agent-files (16/11/2026) - **Đến**: phiên kế tiếp của repo này
- **Trạng thái**: MỞ - phần agent đã xong; mục 2 còn lại là việc 👤 và việc chờ bản phát hành tới
- **Phiếu trước**: `20261115-02` (ĐÓNG; việc còn mở chép sang mục 2 dưới đây)

## 0. Đọc trước

- Repo `tsudev-org/tsufiles`; `gh` luôn kèm `-R tsudev-org/tsufiles` (thiếu cờ thì `gh` hỏi repo upstream
  KnifeLemon/Filee). KHÔNG tạo lại tên `tsudev-files` (ADR-0003 mục 7).
- Quyền commit/push/PR/merge/`wrangler deploy` phải được chủ dự án nói lại trong chat mỗi phiên.

## 1. Đã xong trong phiên

- PR #85 (`ae8a223d`, mục 2.7 phiếu trước): `release.yml` chép bundle của bước "Attest build provenance" và "Attest
  SBOM" thành `tsudev-files-<ver>-provenance.intoto.jsonl` và `tsudev-files-<ver>-sbom.intoto.jsonl`, tải lên bản nháp
  cùng các gói. Tên tệp không trùng hậu tố trình cập nhật tìm (`UpdateService.cs:780`). `docs/vi/PHAT-HANH.md` bước 5
  ghi cách kiểm ngoại tuyến (`gh attestation verify <gói> --bundle <bundle>`). Có hiệu lực từ bản phát hành tới.
- PR #86 (`09535dad`, mục 2.5 phiếu trước - chủ dự án đồng ý trong chat 16/11/2026): bật lock file NuGet.
  - `RestorePackagesWithLockFile` ở `Directory.Build.props`; CI `dotnet restore --locked-mode` (ci.yml ×2,
    security.yml), `-p:RestoreLockedMode=true` cho bước build ở portable.yml và release.yml. Bước
    `publish -r <RID>` vẫn restore thường.
  - BẪY 1: `Avalonia.Wayland` chỉ có trong bản Linux ⇒ `TsuFiles.App`, `App.Tests`, `Setup.Tests` giữ thêm
    `packages.lock.no-wayland.json` (Windows/macOS) qua `Directory.Build.targets` (`TsuFilesLockPerWayland=true`);
    `TsuFilesNativeWayland` chuyển lên `Directory.Build.props`.
  - BẪY 2: `TsuFiles.Setup` có `IsAotCompatible` ⇒ SDK thêm ngầm `Microsoft.NET.ILLink.Tasks` theo bản SDK (WSL
    10.0.403 -> 10.0.14, runner -> 10.0.15) ⇒ Setup tắt lock file riêng; gói của nó đã khóa qua lock của Setup.Tests.
  - BẪY 3: comment XML trong `.props` không được chứa `--`.
  - `tools/cap-nhat-lock-nuget.sh` sinh lại cả hai loại lock file (`--kiem` để kiểm như CI); CONTRIBUTING ghi cách dùng.
  - CI 19/19 xanh ở `0ed1a428` (mọi nền tảng restore ở chế độ khóa); WSL: `--kiem` khớp, build Release 0 lỗi.

## 2. Việc tiếp theo (👤 = chỉ chủ dự án)

- [ ] 2.1. Agent: mỗi PR Dependabot NuGet (tháng 12/2026) - Dependabot chỉ cập nhật `packages.lock.json` (Linux), CI
  Windows/macOS sẽ đỏ vì `packages.lock.no-wayland.json` cũ: checkout nhánh, chạy `tools/cap-nhat-lock-nuget.sh`,
  commit lock file, đẩy lên rồi mới gộp.
- [ ] 2.2. Agent: đo lại Scorecard sau khi lịch chạy lại (`curl -s
  https://api.securityscorecards.dev/projects/github.com/tsudev-org/tsufiles`). Kỳ vọng Pinned-Dependencies 10 (alert
  code scanning #17 tự đóng); Signed-Releases chỉ lên 10 sau bản phát hành tới có `*.intoto.jsonl`.
- [ ] 2.3. Agent, ở bản phát hành tới: xác nhận bản nháp có hai tệp `*.intoto.jsonl` và
  `gh attestation verify <gói> -R tsudev-org/tsufiles --bundle <tệp>` đạt.
- [ ] 2.4. 👤 Secret `WINGET_TOKEN` (PAT `public_repo`) - có rồi thì agent nộp winget qua komac.
- [ ] 2.5. 👤 Cloudflare JavaScript Detections cho `files.tsudev.com` (quyết định 10/11/2026 là KHÔNG tắt; chủ dự án chọn lại).
- [ ] 2.6. 👤 #42 (repo `tsudev-org/tsufiles-assets`) và #44 (khóa ký dự phòng).
- [ ] 2.7. 👤 SignPath (D5) khi đủ ngưỡng.
- [ ] 2.8. 👤 Ruleset bắt buộc 1 người duyệt (Branch-Protection/Code-Review) - chỉ khi có người duyệt thứ hai.

## 3. Cổng kiểm

- `scripts/check-standards.sh` ĐẠT (3 lưu ý cũ). YAML 4 workflow hợp lệ. Website không đổi - không deploy.
