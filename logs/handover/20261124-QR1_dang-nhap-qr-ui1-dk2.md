# PHIẾU YÊU CẦU QR1 + UI1 + DK2 - tsufiles

- **Mã phiếu**: 20261124-QR1 (bản sao cho repo) - **Từ**: điều phối `projects-71`, 24/11/2026 - **Trạng thái**: MỞ
- **Phiếu gốc** (đọc phần liên quan): `~/projects/logs/handover/20261124-QR1_dang-nhap-qr-giao-dien-thong-nhat-toan-he.md`,
  `20261124-UI1_bieu-tuong-kho-thay-toan-he.md`, `20261124-DK2_dang-ky-bct-tin-nhiem-mang-chong-lua-dao.md`.
- **Chuẩn**: tsudev-standards **v5.1.0** - `CROSS_DEVICE_LOGIN.md`, `AUTH_AND_ACCOUNT.md` 3.0/13.2/13.4,
  `DESIGN_SYSTEM.md` 1 (biểu tượng trên viên chỉ báo), `LANGUAGE_SELECTION.md` 5.1, `PROJECT_STRUCTURE.md` 3.1,
  `BRAND_ASSETS.md` 12.3.
- **Hạng / loại**: B - Web + desktop .NET

## Việc của repo này

1. **QR4**: màn đăng nhập 13.2, nút mở trình duyệt hệ thống tới `/dang-nhap` IdP (Đ2 có sẵn); web files.tsudev.com theo 13.2 + ô đồng ý; **UI1** desktop + web; DK2 footer files.tsudev.com
2. **STD-SYNC** trong PR kế tiếp (không PR riêng): sync v5.1.0, bật cổng Clean Codebase (mã chết/phụ thuộc thừa) và
   ngân sách tốc độ; liên kết về tsudev.com kèm dấu hiệu TsuDEV + favicon đủ bộ.
3. Rà **UI1** cả Sáng/Tối: biểu tượng trên nền thật ở mọi trạng thái ≥ 3:1, thêm test chặn tái phát (nếu có giao diện).

Repo có phiên riêng ⇒ phiên đó làm; ghi kết quả một dòng dưới đây + dòng tương ứng ở phiếu gốc; `akb` theo chuẩn.

## Kết quả xử lý

- 24/11/2026 - projects-71: mở phiếu.
- 25/11/2026 - tsufiles-e9: STD-SYNC xong (v5.4.0, cùng PR STD50). UI1 desktop: test
  `Selected_nav_icon_reads_on_its_indicator_pill` đo biểu tượng/chữ mục đang chọn trên viên chỉ báo (primary 18%) phủ
  trên 3 nền, Sáng + Tối (đạt ≥ 4.5:1); web có `check-a11y` 2 chế độ. QR4 (màn đăng nhập 13.2) + DK2 footer: CHƯA làm -
  TsuFiles chưa có tài khoản/đăng nhập, làm cùng PRO1/PRO2.
