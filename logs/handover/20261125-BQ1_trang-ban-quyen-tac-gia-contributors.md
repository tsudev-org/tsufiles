# PHIẾU BQ1 - Trang "Bản quyền": tác giả, đồng tác giả, contributors, các phiên bản

- **Mã phiếu**: 20261125-BQ1 - **Từ**: phiên `tsudev-7d` (repo tsudev), 25/11/2026 - **Trạng thái**: MỞ
- **Phiếu gốc**: `~/projects/tsudev/logs/handover/20261125-ECO1_nhan-open-private-ho-so-ung-ho-go-pro.md` (mục 1.2, 5)
- **Yêu cầu chủ dự án (25/11/2026)**: minh bạch quyền tác giả - người dùng phân biệt được sản phẩm do cá nhân/cộng đồng
  xây; đáp ứng điều khoản giấy phép và pháp luật về quyền tác giả.

## Việc

1. **files.tsudev.com/ban-quyen** hiện ĐẦY ĐỦ:
   - Tác giả: TsuDEV (Nguyễn Trang Tình Sử) - repo `tsudev-org/tsufiles`.
   - Đồng tác giả: repo gốc mà TsuFiles fork (xem NOTICE/README của repo) - tác giả gốc là ĐỒNG TÁC GIẢ.
   - Contributors: danh sách tài khoản GitHub có đóng góp ở repo gốc VÀ repo `tsudev-org/tsufiles` (ảnh đại diện, tên, liên kết hồ
     sơ, số commit), lọc bot (`[bot]`, dependabot, github-actions).
   - Các phiên bản: danh sách Release (tag, ngày DD/MM/YYYY, liên kết ghi chú phát hành), đánh dấu bản mới nhất.
2. **Nguồn số liệu (0 đồng, không gọi GitHub lúc người dùng mở trang)**: script gọi GitHub REST
   `GET /repos/{owner}/{repo}/contributors` + `/releases` (trong CI dùng `GITHUB_TOKEN`), ghi JSON vào repo; Action
   hằng tuần mở PR khi đổi (ghim SHA action, quyền tối thiểu). Không có web thì nhúng vào màn hình "Giới thiệu/Bản
   quyền" của app.
3. Giữ nguyên NOTICE/LICENSE và ghi công tác giả gốc theo giấy phép; trang chỉ BỔ SUNG, không thay NOTICE.
4. Định dạng dòng ngắn dùng chung toàn hệ (tsudev.com hiện trên thẻ app): `Author: TsuDEV + N Contributors` - N là số
   contributor KHÁC chủ dự án (đếm cả repo gốc).
5. Giao diện theo `.standards` (Sáng · Tối · Hệ thống, tương phản AA), không ghi "do AI".

## Kết quả xử lý

- 25/11/2026 - tsudev-7d: mở phiếu.
