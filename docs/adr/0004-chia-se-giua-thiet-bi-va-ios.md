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
| S3 | Tìm máy TsuFiles khác trong mạng (UDP multicast), gửi thẳng giữa desktop | - | 0 |
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

### 2.3. Tương thích LocalSend (cân nhắc ở S3)

LocalSend (MIT) có giao thức mở (UDP multicast + REST) và đã có app iOS miễn phí. Nếu TsuFiles nói được giao thức này
thì iPhone có app LocalSend gửi/nhận được với TsuFiles mà tsudev không cần phát hành app iOS. Trước khi làm phải kiểm:
phiên bản iOS tối thiểu của app LocalSend hiện hành và giấy phép của đặc tả giao thức.

## 3. Hệ quả

- S1-S3 không đổi khóa JSON của dòng lệnh, không đổi trình cập nhật. Thêm lệnh `tsufiles share` (S1) cho máy chủ không
  màn hình.
- Tăng bề mặt tấn công (cổng mạng mở): thêm fuzz cho bộ đọc multipart (cùng khuôn P4) và test chặn địa chỉ công khai.
- S4 chỉ làm khi chủ dự án chấp thuận Mac + tài khoản Apple; nếu không, S2 (và S3 với LocalSend) là bản iOS chính thức.

## 4. Kết quả S3 (18/11/2026)

Khảo sát LocalSend (mục 2.3) trước khi làm:

- **iOS tối thiểu**: app LocalSend nhắm `IPHONEOS_DEPLOYMENT_TARGET = 15.0` (đọc ở mã fork Lotus Share của hệ) ⇒ iPhone 8
  (iOS 16) cài được app LocalSend miễn phí trên App Store.
- **Giấy phép**: repo đặc tả `localsend/protocol` không ghi giấy phép (đo bằng GitHub API 18/11/2026); app LocalSend là
  MIT. TsuFiles chỉ **viết lại** phần giao thức cần thiết từ mô tả để tương tác (không chép văn bản đặc tả hay mã
  LocalSend) - giao thức mạng là giao diện, không phải mã được chép.
- **Lotus Share** (fork LocalSend của hệ) đã đổi tiền tố API thành `/api/lotus/v2` ⇒ không cùng dây với LocalSend gốc;
  TsuFiles nhắm LocalSend gốc vì đó là app có sẵn trên iOS.

Quyết định: **TsuFiles nói giao thức LocalSend v2** thay vì tự đặt giao thức multicast riêng - một lần làm, vừa tìm
được TsuFiles khác vừa nói chuyện với app LocalSend trên iPhone/Android/máy tính.

| Phần | Nội dung |
| --- | --- |
| S3a | `Core/Share/LocalSend/`: thông báo/nghe UDP multicast `224.0.0.167:53317`, trả lời bằng `POST /register`; `ShareServer` nhận `/api/localsend/v2/{register,info,prepare-upload,upload,cancel}` (chế độ HTTP, ưu tiên cổng TCP 53317); `LocalSendClient` gửi tới máy HTTP hoặc HTTPS (chứng thư tự ký chỉ được tin khi SHA-256 trùng vân tay đã công bố); lệnh `tsufiles send`; `tsufiles share` bật LocalSend |
| S3b | Giao diện: danh sách máy tìm thấy + nút Gửi ở trang "Chia sẻ với điện thoại"; hộp duyệt hiện tên thiết bị |

Bảo mật bổ sung cho tuyến LocalSend (không có mã phiên trong URL vì giao thức không có): `/prepare-upload` đi qua cùng
bước duyệt thiết bị; mỗi tệp một token 128 bit dùng một lần, chỉ từ đúng địa chỉ đã xin; một phiên nhận tại một thời
điểm (409); thân JSON tối đa 1 MB, tối đa 10 000 tệp; `Content-Length` phải đúng kích thước đã khai; có `sha256` thì
kiểm (422). Không hỗ trợ PIN: duyệt bằng tay thay PIN. Chưa có chế độ HTTPS phía nhận (giới hạn của mục 2.1 giữ nguyên).
