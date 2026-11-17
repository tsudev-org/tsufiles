# Chia sẻ với điện thoại

Gửi tệp giữa máy tính chạy TsuFiles với điện thoại (iPhone, Android), máy tính bảng hay máy tính khác **cùng mạng
Wi-Fi**. Điện thoại chỉ cần camera và trình duyệt, không phải cài ứng dụng. Chạy được trên iPhone đời cũ (iPhone 8,
Safari 16). Thiết kế: [ADR-0004](../adr/0004-chia-se-giua-thiet-bi-va-ios.md).

## 1. Dùng trong app

1. Mở TsuFiles, chọn **Chia sẻ với điện thoại** ở thanh bên trái.
2. Bấm **Bật chia sẻ**. Màn hình hiện mã QR và địa chỉ dạng `http://192.168.1.23:53817/s/<mã phiên>/`.
3. Trên điện thoại: mở camera, quét mã QR, chạm vào đường dẫn hiện ra (hoặc gõ địa chỉ vào trình duyệt).
4. Lần đầu mỗi thiết bị kết nối, máy tính hỏi "iPhone (192.168.1.5) muốn kết nối. Cho phép?". Bấm **Cho phép**.
   Nếu cửa sổ TsuFiles đang ẩn ở khay, app tự mở trang chia sẻ để bạn duyệt. Không trả lời trong 2 phút thì coi
   như từ chối.
5. Gửi từ điện thoại lên máy tính: chọn tệp trên trang web rồi gửi. Tệp lưu vào thư mục ở mục **Tệp nhận được lưu
   vào** (mặc định `Tải xuống/TsuFiles`; bấm **Đổi thư mục** khi chưa bật chia sẻ, app nhớ cho lần sau).
6. Gửi từ máy tính xuống điện thoại: kéo thả tệp vào ô **Tệp điện thoại tải về được** (hoặc bấm **Thêm tệp**).
   Điện thoại tải lại trang để thấy tệp mới.
7. Xong thì bấm **Tắt chia sẻ**. Không dùng 15 phút thì chia sẻ tự tắt. Mỗi lần bật tạo mã mới, mã cũ hết hiệu lực.

Máy chủ không màn hình: dùng lệnh `tsufiles share` (xem `tsufiles help share`).

## 2. Khi điện thoại không mở được trang

- Điện thoại và máy tính phải cùng một mạng Wi-Fi (Wi-Fi khách của quán cà phê/khách sạn thường chặn các máy nói
  chuyện với nhau).
- **Windows hỏi tường lửa lần đầu bật chia sẻ**: chọn cho phép ở **Mạng riêng (Private)**. Nếu lỡ bấm hủy: Cài đặt
  Windows > Quyền riêng tư và bảo mật > Bảo mật Windows > Tường lửa > Cho phép ứng dụng qua tường lửa > đánh dấu
  TsuFiles ở cột Private. Mạng đang để chế độ **Công cộng (Public)** thì đổi sang Riêng tư trong cài đặt Wi-Fi.
- Máy có nhiều card mạng (VPN, máy ảo): trang chia sẻ hiện thêm các địa chỉ khác bên dưới mã QR, thử gõ từng địa
  chỉ. Vừa đổi Wi-Fi thì bấm nút làm mới cạnh **Tắt chia sẻ**.
- Trang báo "Máy tính đã từ chối thiết bị này": tắt rồi bật lại chia sẻ (phiên mới hỏi lại từ đầu).

## 3. An toàn

- Chỉ dùng trên mạng tin cậy (nhà, cơ quan): đường truyền trong mạng nội bộ là HTTP, chưa mã hóa.
- Chia sẻ tắt sẵn; chỉ nhận kết nối từ địa chỉ mạng nội bộ; đường dẫn chứa mã phiên ngẫu nhiên 128 bit; mỗi thiết
  bị mới phải được bạn cho phép.
- Tệp nhận được làm sạch tên, không bao giờ ghi đè tệp cũ, gắn dấu "tải từ Internet" (Mark-of-the-Web) và từ chối
  loại tệp có thể chạy mã (`.exe`, `.bat`, `.ps1`...). Tối đa 4 GB mỗi tệp.
- Chi tiết kỹ thuật: [KIEN-TRUC-BAO-MAT.md](KIEN-TRUC-BAO-MAT.md) mục 2.
