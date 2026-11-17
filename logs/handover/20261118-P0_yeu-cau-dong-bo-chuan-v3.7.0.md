# PHIẾU YÊU CẦU - P0 phiếu toàn hệ 20261117-P2: đồng bộ bộ quy ước lên v3.7.0

- **Mã phiếu**: 20261118-P0
- **Từ**: phiên điều phối `~/projects` (projects-7e), 18/11/2026
- **Đến**: phiên kế tiếp của repo `tsufiles` (repo đang có phiên khác làm việc nên phiên điều phối KHÔNG sửa hộ)
- **Trạng thái**: ĐÓNG 18/11/2026 - PR #101 (sync v3.7.0 + khối Phiên kế tiếp); V1 "Đã đọc" ở PR #100

## Việc (một PR nhỏ, merge khi CI xanh)

1. `./scripts/sync-standards.sh --ref v3.7.0` (bản MINOR - không cần sửa code; thêm `docs/DISTRIBUTION_TRUST.md`,
   `docs/VERSIONING.md` v3, cổng kiểm 5b chỉ LƯU Ý). Hoặc chạy nguyên script cơ học
   `~/projects/logs/tools/p0-sync-standards.sh ~/projects/tsufiles "hàng 4 (`tsufiles`)"` (làm trên worktree riêng, tự mở PR).
2. Cùng PR: thêm vào đầu `logs/STATE.md` khối "Phiên kế tiếp: đọc
   `~/projects/logs/handover/20261117-P2_ke-hoach-phan-phoi-tin-cay-da-nen-tang.md` hàng 4 (`tsufiles`) ở mục 3 +
   `.standards/docs/DISTRIBUTION_TRUST.md`".
3. Ghi "Đã đọc" phiếu V1 (`logs/handover/20261117-V1_*` mục 6 bản sao trong repo này).
4. Xong: ghi một dòng vào mục 8 phiếu `~/projects/logs/handover/20261117-P2_*` (số PR) và ĐÓNG phiếu này.

Mẫu đã làm ở 13 repo khác: ví dụ tsudev-org/sound-db#12, tsudev-org/tsucontact#32.
