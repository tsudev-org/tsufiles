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
| S3 | Nút "Gửi bằng Lotus Share": giao tệp kết quả cho app Lotus Share đã cài (mục 2.3) | app Lotus Share | 0 |
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

### 2.3. Gửi sang thiết bị khác (S3): giao cho Lotus Share

Hệ TsuDEV đã có sản phẩm chia sẻ tệp riêng là **Lotus Share** (repo `tsudev-org/lotus-share`). TsuFiles KHÔNG tự nói
giao thức mạng của Lotus Share hay của ứng dụng nào khác. Thẻ kết quả ở trang chủ có nút **"Gửi bằng Lotus Share"** (chỉ
hiện khi máy đã cài Lotus Share): TsuFiles mở Lotus Share kèm đường dẫn các tệp kết quả - như người dùng kéo tệp vào
cửa sổ Lotus Share - rồi Lotus Share tìm máy và gửi (`TsuFiles.Core/Share/LotusShare.cs`).

- Tìm Lotus Share chỉ ở chỗ bộ cài chính thức đặt (Windows: `InstallLocation` của khóa gỡ cài đặt AppId
  `{21D09887-EE8E-478B-B43F-BD492DE6EFE3}`, rồi `Program Files`/`%LOCALAPPDATA%\Programs`; Linux: `/opt/lotus-share/`,
  `/usr/bin/lotus-share`; macOS: `Lotus Share.app` trong `/Applications`, `~/Applications`). Không dò PATH.
- Đường dẫn đi qua `ArgumentList` (không qua shell), luôn tuyệt đối nên Lotus Share không hiểu nhầm thành cờ. macOS
  giao qua `open -b com.tsudev.lotusshare` (sự kiện mở tệp). Lotus Share đang chạy thì tiến trình mới chuyển danh sách tệp sang cửa sổ đang mở.
- Hợp đồng duy nhất giữa hai dự án: "mở tệp chạy của Lotus Share với đường dẫn tệp/thư mục ở dòng lệnh". Không chung
  cổng mạng, không có máy chủ thứ hai luôn nghe trong LAN, không buộc lịch phát hành của hai bên.

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

## 5. Chốt S3: giao tệp thay vì nói giao thức (19/11/2026)

Kế hoạch S3 sau mục 4 là TsuFiles tự cài giao thức Lotus v2 (chứng thư thiết bị + mTLS, tìm máy UDP 53420, máy chủ
HTTPS, máy khách). Xét lại cùng ngày, bỏ hướng đó vì:

- **Dư thừa**: viết lại bằng C# một giao thức Lotus Share đã có (Dart/Rust); Lotus Share đã có cả trang "chia sẻ qua
  web" + QR giống S1/S2. Nhu cầu gốc (iPhone 8 gửi/nhận với máy tính) là việc của Lotus Share; phần riêng của TsuFiles
  (điện thoại gửi tệp lên để chuyển đổi rồi tải về) S1/S2 đã đáp ứng.
- **Xung đột**: cài cả hai trên một máy thì danh sách thiết bị của Lotus Share hiện hai mục cho cùng một máy; hai ứng
  dụng cùng nghe UDP 53420 (một bên bind độc quyền là bên kia mất tìm máy); thêm một máy chủ luôn nghe trong LAN cho một
  ứng dụng chuyển đổi ngoại tuyến (dữ liệu D3); mọi thay đổi phá vỡ giao thức của Lotus Share buộc TsuFiles phát hành
  theo.

Chủ dự án 19/11/2026 giao agent chọn phương án tối ưu, không xung đột giữa các dự án ⇒ chọn mục 2.3 (giao tệp). Mã của
#105/#106 (commit ở mục 4) không dùng lại.
