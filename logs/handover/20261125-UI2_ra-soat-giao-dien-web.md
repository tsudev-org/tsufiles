# PHIẾU - UI2: rà quét sâu giao diện web + hiện đại hoá (tsufiles)

- **Mã phiếu**: 20261125-UI2 (phiếu con của `~/projects/logs/handover/20261125-UI2_ra-soat-giao-dien-web-toan-he.md`)
- **Từ**: chủ dự án (25/11/2026), phát bởi phiên `tsudev-3e` - **Đến**: agent của repo `tsufiles`
- **Trạng thái**: MỞ
- **Phạm vi**: A + B (thuộc hệ sinh thái TsuDEV)
- **Ủy quyền**: chủ dự án cho agent TỰ MỞ TRÌNH DUYỆT (Playwright/Chromium headless) để rà website của repo này.

## Việc cần làm

1. Đọc phiếu toàn hệ (đường dẫn ở trên) - mục 0 là ba nguyên nhân "trang nhúng khi Ctrl+F5" đã gặp ở tsudev.com; kiểm
   cả ba ở repo này TRƯỚC (fade-in ở màn đầu, font không preload/dự phòng lệch số đo, scroll-reveal ẩn trước khi đo).
2. Làm phần A (rà quét sâu bằng trình duyệt: quay khung hình tải lạnh, ảnh 3 khổ × Sáng/Tối, console/mạng, tiếng
   Việt, bàn phím, LCP/CLS/INP).
3. Làm phần B (hiện đại hoá) - thay đổi bảng màu/font CHUNG chờ bản phát hành tsudev-standards, không tự đổi hex.
4. Ghi từng lỗi + cách sửa vào mục Kết quả; sửa theo PR nhỏ; `akb new issue` cho lỗi tốn > 10 phút.

## Kết quả xử lý

- 25/11/2026 - tsudev-3e: mở phiếu.
- 25/11/2026 - tsufiles-e9: mục 0 kiểm ở files.tsudev.com: (1) không có fade-in màn đầu, (2) font tự host CHƯA preload ⇒ thêm preload Be Vietnam Pro 400 + Lexend 600 (latin + vietnamese) trong PR #124; `size-adjust` cho font dự phòng chưa đo - phiên sau, (3) không có scroll-reveal. Phần A (quay khung hình, 3 khổ) và B: phiên sau, cùng PR 2 STD50.
- 25/11/2026 - tsufiles-c4: **Phần A** (Playwright headless, production files.tsudev.com + bản build, 12 trang x 360/768/1366 x
  Sáng/Tối; kịch bản rà quét + quay khung hình CDP 150 ms/4 Mbps không cache):
  - Tải lạnh trang chủ: khung đầu tiên có nội dung (~1,2 s) đã đúng font + đúng bố cục, không khung trống/nhảy; CLS
    0,0001-0,0003; LCP 0,7-1,3 s. Không lỗi console/JS, không 404 tài nguyên (trừ trang 404 cố ý), không ảnh vỡ.
  - LỖI 1 `/ban-quyen/` khổ 360: cuộn ngang 9px do URL dài `github.com/TolikPylypchuk/libuiohook` trong đoạn văn ⇒
    `.van-ban :is(p, li) > a { overflow-wrap: anywhere }`. Đo lại: 0px cả 3 khổ.
  - LỖI 2 `public/theme.js`: hai trình xử lý cùng bắt `[data-chep]` - trình của trang Ủng hộ chép CHUỖI id
    ("lenh-windows-0") rồi trình ủy quyền mới ghi đè bằng lệnh thật (đúng nhờ thứ tự, dễ gãy) ⇒ trình Ủng hộ bỏ qua nút
    có `data-chep` là id phần tử. Thử thật: nút Chép lệnh Scoop ở trang chủ chép đúng lệnh.
  - Mục 0.2 font dự phòng: `@font-face 'Be Vietnam Pro Fallback'` ĐO THẬT trên văn bản trang chủ bằng fontTools so với
    Arimo (cùng số đo Arial): nét 400 size-adjust 107,1% ascent 93,3% descent 24,7%; nét 600/700 102,9% / 97,1% / 25,7%
    (Lexend 600 so Arial Bold 105,1% - chưa áp vì `--font-display` thuộc chuẩn). Kiểm bằng Chromium chặn woff2 + Arimo
    qua fontconfig tạm: đoạn văn 597px so với 602px (trước 579px, một đoạn xuống 2 dòng), số dòng khớp. Bẫy:
    `local('Arimo')` không khớp, phải dùng tên đầy đủ/PostScript.
  - a11y (axe 12 trang x 2 chế độ) 0 lỗi; check-theme 0 lỗi.
  - **Phần B**: B2 hoạ tiết tĩnh hero = `quy-dao` (STD50 PR 2). B1 nền Sáng dịu + B4 đổi font: CHỜ bản tsudev-standards mới
    (không tự đổi hex). B3: thẻ không có dải viền màu trên đầu; hoạ tiết hình học cho nền thẻ - chờ chuẩn. Phiếu còn MỞ
    tới khi sync bảng màu mới.
- 25/11/2026 - tsufiles-c4: #126 gộp + deploy `22a79ee0`; rà lại production 12 trang x 3 khổ x Sáng/Tối 0 lỗi, LCP 0,9-1,5 s
  (phần tử LCP là h1), CLS <= 0,0003.
