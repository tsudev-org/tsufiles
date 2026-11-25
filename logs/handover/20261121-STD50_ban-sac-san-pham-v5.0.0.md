# PHIẾU YÊU CẦU - Bản sắc riêng của TsuFiles (tsudev-standards v5.0.0)

- **Mã phiếu**: 20261121-STD50
- **Từ**: phiên tsudev-standards, 21/11/2026 - **Đến**: phiên kế tiếp của `tsufiles`
- **Trạng thái**: ĐÓNG 25/11/2026 (PR 1 #124, PR 2 nhánh `feat/std5-cong-cu-quy-dao`)
- **Nguồn**: https://github.com/tsudev-org/tsudev-standards/releases/tag/v5.0.0 - `docs/DESIGN_SYSTEM.md` mục 1.2,
  CHANGELOG 5.0.0 "Hướng dẫn nâng cấp"
- **Bối cảnh**: chủ dự án muốn mỗi sản phẩm có đặc trưng nổi bật riêng (màu, giao diện, bố cục). Trước v5.0.0, 9/11
  sản phẩm dùng cùng một màu xanh. Từ v5.0.0: nền/chữ/trạng thái/tiếp cận là chung; màu nhấn, bố cục, bo góc, hoạ
  tiết, font tiêu đề là riêng, khai ở `.standards/tokens/products/files.json` - KHÔNG tự khai lại tại repo này.

## Bản sắc đã chốt cho `tsufiles`

| Trường | Giá trị |
| --- | --- |
| `data-product` | `files` |
| Màu chính Tối / Sáng | `#A970FF` / `#7C3AED` (hue OKLCH 293; lý do: tím logo cú) |
| Bố cục `layout` | `cong-cu` - vùng thao tác chính (kéo thả/chọn tệp/nút chạy) ngay màn đầu, căn giữa; giới thiệu nằm dưới |
| Bo góc `shape` | `mem` - 6/10/14px, nút 10px |
| Hoạ tiết `motif` | `quy-dao` (tĩnh, ở hero + `og-image`, phủ ≤ 15%) |
| Font tiêu đề | Lexend (tự host woff2 bộ `vietnamese`, tối đa 2 độ đậm) |

Muốn đổi bất kỳ giá trị nào: PR vào tsudev-standards (sửa `hue`/`layout`... + chạy `gen-product-palette.mjs`), cổng
kiểm chống trùng phải xanh. KHÔNG ghi đè tại repo này.

## Việc cần làm - chia hai PR, mỗi PR một phiên

**PR 1 - màu + hình** (`feat(ui): bản sắc files theo tsudev-standards v5.0.0`):
1. Sync `.standards/` lên v5.0.0 (bước `curl` sync-standards.sh trước, theo `docs/SYNC.md`).
2. Web: nạp `.standards/tokens/tokens.css` rồi `.standards/tokens/products.css`; `<html data-product="files">` (cạnh
   `data-theme`). Desktop/di động: gộp đè `products/files.json` lên `color.light`/`color.dark` lúc build.
3. Xoá bảng màu tự khai (`palette.css`, `extensions.*.json`, dải `--color-*` riêng) và mã màu cứng còn sót.
4. Giá trị đã lưu `warm` đọc thành `light` (khối `warm` đã gỡ khỏi token).
5. Nút dùng `var(--radius-control)`; h1-h3 và số liệu lớn dùng `var(--font-display)`.
6. Ảnh chụp trước/sau, Sáng + Tối, đính vào PR. Cổng kiểm + test xanh.

**PR 2 - bố cục** (`feat(ui): bố cục cong-cu + hoạ tiết quy-dao`):
1. Dựng phần THÂN trang chủ website / màn hình đầu app theo `cong-cu`; header, khu tài khoản, chân trang, bộ chọn
   giao diện giữ theo `ECOSYSTEM_IDENTITY.md` mục 4.
2. Hoạ tiết `quy-dao` tĩnh ở hero, vẽ bằng `accent`/`glow`; sinh lại `og-image.png` cùng hoạ tiết.
3. Ảnh chụp trước/sau, Sáng + Tối.

## Việc riêng của repo này

- Màu KHÔNG đổi: `files.json` là bảng tím logo cú 4.2.1 (`locked`). Việc còn lại: bo góc `mem`, font tiêu đề, bố cục, hoạ tiết.

## Kết quả xử lý

-
- 25/11/2026 - tsufiles-e9: **PR 1 xong** (nhánh `feat/std5-ban-sac-files`): sync chuẩn v5.4.0; website nạp
  `.standards/tokens/tokens.css` + `products.css`, `<html data-product="files">`, xoá `styles/tokens.css` + `palette.css`;
  bo góc `--radius-*` (mem), nút `--radius-control`, h1-h3 Lexend tự host (500/600, latin/latin-ext/vietnamese);
  website mặc định giao diện Hệ thống (PRODUCT_POLICY 1.2). Desktop: `StandardTokens` nhúng `design-tokens.json` +
  `products/files.json` lúc build, `NightOwlPalette` đọc từ đó (tím đổi từ #A06BF5/#6A2BD0 sang chuẩn #A970FF/#7C3AED,
  bề mặt theo lớp nền chung, viền nút `border-control`); test `StandardTokensTests`. Font Lexend chưa nhúng vào app
  desktop (tiêu đề app vẫn font thân). **PR 2 (bố cục cong-cu + hoạ tiết quy-dao + og-image) CHƯA làm - phiên sau.**
- 25/11/2026 - tsufiles-c4: **PR 2 xong** (nhánh `feat/std5-cong-cu-quy-dao`): trang chủ theo `cong-cu` - khối thao tác căn
  giữa ngay màn đầu (ví dụ đường chuyển đổi, nút "Tải cho Windows" kèm dung lượng bộ cài, liên kết hệ khác tới đúng
  neo `/tai-ve/#goi-*`, lệnh Scoop có nút Chép), ảnh ứng dụng xuống dưới (`loading=lazy`). Hoạ tiết `quy-dao` tĩnh (SVG
  nội tuyến, `--accent`, nét 16%/điểm 14%) ở hero; `og-image.png` sinh lại có vòng quỹ đạo, màu đọc từ
  `.standards/tokens/products/files.json` (bỏ mã màu chép tay trong `tao-tai-san.py`). Ảnh trước/sau 1366 + 360 x
  Sáng/Tối + og-image: `docs/anh-chup/20261125-std50-pr2/`. App desktop: màn đầu (Trang chủ) đã là vùng thả tệp lớn
  căn giữa = `cong-cu`, không đổi; font Lexend vẫn chưa nhúng vào app (tiêu đề app dùng font thân).
