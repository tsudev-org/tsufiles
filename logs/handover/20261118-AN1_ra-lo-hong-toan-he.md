# PHIẾU YÊU CẦU - AN1 rà lỗ hổng toàn hệ: kết quả cho tsufiles

- **Mã phiếu**: 20261118-AN1 (bản gốc: `~/projects/logs/handover/20261118-AN1*`)
- **Từ**: phiên điều phối `projects-e4`, 18/11/2026 (chép lại bởi phiên tsufiles 17-18/11 lúc đóng phiên)
- **Đến**: phiên tsufiles kế tiếp
- **Trạng thái**: ĐÓNG 18/11/2026 - PR #103 (`.gitleaksignore`, website astro/marked→devDependencies, cổng `npm audit --omit=dev`); dòng tsufiles ở phiếu gốc đã ghi

## Kết quả rà (do phiên điều phối đo)

- Dependabot: 0 cảnh báo mở; secret scanning: 0.
- gitleaks toàn lịch sử: 6 phát hiện, đều GIẢ (token mẫu 32 ký tự ở `tests/Filee.App.Tests/UpdateServiceTests.cs:920-948`
  của lịch sử cũ, tên thẻ ExifTool `GPSCoordinates`, biến `token` ở `GestureDetector.cs:79`).
- Code scanning: cảnh báo Scorecard `VulnerabilitiesID` (#28) - OSV thấy lỗ hổng trong một lock file. Phần còn lại là
  cảnh báo chính sách của Scorecard (BranchProtection, CodeReview, Fuzzing...).

## Việc (một PR nhỏ)

1. Thêm `.gitleaksignore` theo fingerprint của 6 phát hiện giả (chạy gitleaks với `--report-format json` lấy
   `Fingerprint`; kiểm từng dòng trước khi bỏ qua). Lưu ý `~/.claude/CLAUDE.md`: gitleaks trong org dùng binary ghim
   sha256, không dùng `gitleaks/gitleaks-action`.
2. Chạy `osv-scanner` trên repo để biết gói nào ở cảnh báo #28; gói NuGet thì nâng phiên bản rồi chạy
   `tools/cap-nhat-lock-nuget.sh`; gói npm ở `website/` thì `npm update` gói đó.
3. Xong: ghi một dòng vào mục "Kết quả rà" phiếu AN1 gốc ở `~/projects/logs/handover/` và ĐÓNG phiếu này.
