# ADR-0004: Chia sẻ tệp giữa các thiết bị và bản cho iPhone (kể cả iPhone 8)

- **Trạng thái**: Đề xuất (17/11/2026). Phase S1-S3 agent tự làm; S4 chờ chủ dự án quyết (chi phí).
- **Yêu cầu**: chủ dự án 16/11/2026 - bản cho smartphone iOS, máy cũ như iPhone 8 vẫn tìm thấy, gửi và nhận dữ liệu
  với máy tính và thiết bị khác.

## 1. Bối cảnh

- TsuFiles hiện chỉ có bản desktop (Windows/Linux/macOS) và dòng lệnh. Chưa có mã nào cho việc tìm thiết bị hay gửi
  tệp qua mạng (`src/TsuFiles.Core/Web/` chỉ tải tệp từ URL).
- iPhone 8 dừng ở **iOS 16** (Safari 16). Mọi giải pháp phải chạy trên iOS 16, không chỉ iOS mới nhất.
- App iOS gốc cần: máy Mac (Xcode ký và đóng gói), tài khoản Apple Developer **99 USD/năm**, duyệt App Store. Điều
  này trái nguyên tắc hạ tầng 0 đồng (`.standards/docs/FREE_TIER_STACK.md`), và theo quyết định 12/11/2026 bản iOS phải
  ẩn hoàn toàn phần Ủng hộ.
- Dữ liệu là tệp người dùng (mức D3): không được đi qua máy chủ ngoài, chỉ trong mạng nội bộ.

## 2. Quyết định đề xuất

Làm theo bốn phase, mỗi phase một PR. Mỗi phase dùng được ngay mà không cần phase sau:

| Phase | Nội dung | iPhone 8 dùng được bằng | Chi phí |
| --- | --- | --- | --- |
| S1 | Máy chủ chia sẻ trong mạng LAN chạy trong app desktop (`TsuFiles.Core/Share/`) | - | 0 |
| S2 | Trang web gửi/nhận do chính app desktop phục vụ, mở bằng cách quét mã QR | Safari 16 + Camera | 0 |
| S3 | Tìm máy và gửi thẳng bằng giao thức Lotus Share của hệ (mục 2.3) | app Lotus Share (khi có bản iOS) | 0 |
| S4 | App iOS gốc (Avalonia iOS, .NET 10 hỗ trợ iOS 12.2+) | App Store | Mac + 99 USD/năm |

**S2 là lời giải chính cho iPhone 8**: không cần cài gì, không cần App Store, chạy trên mọi điện thoại có trình duyệt
(iOS, Android, máy tính khác). Điện thoại còn được chuyển đổi tệp nhờ máy tính, ví dụ gửi ảnh HEIC từ iPhone rồi nhận
lại JPG/PDF.

### 2.1. Bảo mật (bắt buộc từ S1)

- Mặc định TẮT; người dùng bật bằng tay, tự tắt sau 15 phút không dùng.
- Chỉ lắng nghe trên địa chỉ mạng nội bộ (RFC 1918, link-local, ULA IPv6); từ chối kết nối từ địa chỉ công khai.
- Mã phiên ngẫu nhiên 128 bit nằm trong URL của mã QR; thiết bị mới phải được duyệt trên máy tính ("iPhone của X muốn
  gửi 3 tệp - Cho phép?").
- Tên tệp làm sạch (chặn `..`, đường dẫn tuyệt đối, ký tự cấm Windows), giới hạn dung lượng, lưu vào thư mục người dùng
  chọn, gắn Mark-of-the-Web (`MarkOfTheWeb.cs` sẵn có) cho tệp nhận.
- Trang web: CSP `default-src 'self'`, không script ngoài, không cookie bên thứ ba.
- Giới hạn đã biết: HTTP trong LAN chưa mã hóa đường truyền (Safari 16 không có `crypto.subtle` ngoài ngữ cảnh an toàn,
  còn chứng thư tự ký thì Safari cảnh báo). Hiện rõ cảnh báo "chỉ dùng trong mạng tin cậy" và ghi vào
  `docs/vi/KIEN-TRUC-BAO-MAT.md`. Phương án HTTPS (chứng thư tự ký + vân tay trong QR) cân nhắc ở S3.

### 2.2. Máy chủ HTTP (S1)

`HttpListener` trên Windows đi qua http.sys: nghe trên địa chỉ LAN cần đặt chỗ URL bằng quyền quản trị (`netsh http
add urlacl`) - không dùng được cho app cài ở mức người dùng. Hai lựa chọn: (a) Kestrel (`Microsoft.AspNetCore.App`,
tăng kích thước gói tự chứa, cần kiểm cắt tỉa), (b) máy chủ HTTP/1.1 tối giản trên `TcpListener` chỉ hỗ trợ đúng các
đường dẫn của S2 (GET trang/tệp, POST multipart có giới hạn). Đề xuất (b) để giữ gói nhỏ, kèm fuzz bộ phân tích; S1
đo cả hai trước khi chốt. Tường lửa Windows sẽ hỏi lần đầu bật chia sẻ - ghi vào hướng dẫn.

### 2.3. Tìm máy và gửi thẳng (S3): giao thức Lotus Share

Hệ tsudev đã có sản phẩm chia sẻ tệp riêng là **Lotus Share** (repo `tsudev-org/lotus-share`, giao thức Lotus v2: cổng
53420, nhóm multicast `224.0.0.168`, API `/api/lotus/v2/`). TsuFiles nói đúng giao thức này để thấy và gửi tệp với Lotus
Share và với TsuFiles khác; KHÔNG nói giao thức của ứng dụng ngoài hệ. Hợp đồng giao thức do lotus-share công bố
(`docs/GIAO-THUC-LOTUS-V2.md`, phiếu `20261119-TC1`); TsuFiles không chiếm cổng TCP 53420 của Lotus Share (máy chủ dùng
cổng riêng khai trong gói), chỉ cùng nghe UDP 53420.

## 3. Hệ quả

- S1-S3 không đổi khóa JSON của dòng lệnh, không đổi trình cập nhật. Thêm lệnh `tsufiles share` (S1) cho máy chủ không
  màn hình.
- Tăng bề mặt tấn công (cổng mạng mở): thêm fuzz cho bộ đọc multipart (cùng khuôn P4) và test chặn địa chỉ công khai.
- S4 chỉ làm khi chủ dự án chấp thuận Mac + tài khoản Apple; nếu không, S2 (trình duyệt) và app Lotus Share là đường
  dùng trên iPhone.

## 4. Sửa hướng S3 (19/11/2026)

Bản S3 đầu tiên (#105, #106, gộp 18/11, chưa phát hành) nói giao thức của một ứng dụng chia sẻ ngoài hệ và hướng người
dùng cài ứng dụng đó: TsuFiles không thấy Lotus Share, lại chiếm cổng của ứng dụng kia (đụng nhau khi cài cùng máy). Chủ
dự án 19/11 chốt: mọi sản phẩm hệ chỉ dùng tên gọi và giao thức của hệ. #105, #106 được revert toàn bộ (phiếu
`20261119-TC1`); S3 làm lại theo mục 2.3 khi lotus-share công bố đặc tả. Phần dùng lại được (gia cố JSON từ máy lạ, trần
số lần trả lời gói thông báo, giao diện danh sách máy + nút Gửi) lấy từ lịch sử git: commit `095fd1e5`, `45bc5792`,
`0aec5313`.
