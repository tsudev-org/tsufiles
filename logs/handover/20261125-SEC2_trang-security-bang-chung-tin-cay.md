# PHIẾU YÊU CẦU SEC2 - Trang `/security` với bằng chứng tin cậy TỰ KIỂM CHỨNG ĐƯỢC

- **Mã phiếu**: 20261125-SEC2 (bản sao cho repo) - **Từ**: phiên `tsudev-8d`, 25/11/2026, theo yêu cầu chủ dự án - **Trạng thái**: MỞ
- **Phiếu gốc (ĐỌC mục 0-2 TRƯỚC)**: `~/projects/logs/handover/20261125-SEC2_trang-security-bang-chung-tin-cay-toan-he.md`
- **Website của repo**: files.tsudev.com

## Việc của repo này

E4 security.txt (Contact security@tsudev.com, test Expires) · E5 SSL Labs · E6 Observatory · trang /security thật (mục 2 phiếu gốc) · E8 SignPath (app Windows).

## Ràng buộc (tóm tắt phiếu gốc mục 0)

- TUYỆT ĐỐI không nhãn trang trí: mỗi mục có liên kết tới trang kết quả của bên thứ ba + ngày đo; không có ⇒ không hiện.
- Chỉ khai sự thật; điểm thấp hiện đúng số; không áp dụng ⇒ ghi lý do.
- Số liệu lấy từ tệp trong repo lúc build (không gọi API ngoài khi người xem mở trang).
- Biểu mẫu (Best Practices, SignPath, SSL Labs, Observatory, HSTS preload): mở Edge hồ sơ `tsudev-bp` theo phiếu gốc mục 0.4;
  đăng nhập/OTP/giấy tờ ⇒ 👤 tự nhập. Mỗi lúc một phiên dùng hồ sơ; đóng Edge khi xong.
- Làm trong PR kế tiếp của repo nếu nhỏ; xong ghi một dòng dưới đây + cập nhật hàng của repo ở bảng mục 3 phiếu gốc; `akb` theo chuẩn.

## Kết quả xử lý

- 25/11/2026 - tsudev-8d: mở phiếu.
- 25/11/2026 - tsufiles-e9: E4 security.txt có `mailto:security@tsudev.com`, Policy ⇒ `/security/`, cổng Expires
  30-366 ngày trong `check-dist.mjs`; trang `/security/` + `docs/security/trust.json` (E1 16719 passing 100%, E2 7.2, E5
  SSL Labs A+ x4, E6 Observatory A+ 140 12/12, E3 Release ký + VirusTotal 0/74, E10); liên kết chân trang. E8 SignPath:
  CHƯA nộp đơn (ghi "Chưa có" trên trang) - phiên sau điền đơn qua Edge `tsudev-bp`.
- 25/11/2026 - tsufiles-c4: E8 SignPath - đo ngưỡng phiếu toàn hệ `20261113-02` mục 5: **1/6** (chỉ đạt mục 5: Scorecard
  7.2 + Best Practices passing; 0 sao, 0 fork, 0 người đóng góp ngoài - 60 tác giả trên GitHub là commit upstream
  Filee; 2 bản phát hành trong 2 tuần; 1.160 lượt tải chưa thành xu hướng) ⇒ **KHÔNG nộp** (SignPath đã từ chối một dự án
  TsuDEV 12/11 vì thiếu uy tín; agent không nộp thay chủ dự án - mục 0.6). Đã chuẩn bị: `docs/CODE_SIGNING_POLICY.md`
  + trang `/code-signing-policy/` (liên kết từ chân trang mọi trang + trang tải; nêu ĐÚNG việc kiểm cập nhật mặc định
  bật nên không dùng nguyên câu quyền riêng tư mẫu), `tools/signpath/don.json` + `nop-don.mjs` (chép tsucontact; chưa
  chạy thử vì sẽ gõ họ tên/email thật vào HubSpot), `trust.json` E8 nhắc tới trang chính sách (không gắn liên kết: cổng build chỉ cho liên kết bên thứ ba). 👤 khi đạt >= 4/6:
  `cd website && node ../tools/signpath/nop-don.mjs --headed` (xem ảnh) rồi `--submit`, xác nhận email.
