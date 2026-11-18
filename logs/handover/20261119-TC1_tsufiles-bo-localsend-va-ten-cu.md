# PHIẾU YÊU CẦU - TC1 (tsufiles): rút giao thức LocalSend, nối Lotus Share; bỏ tên cũ còn hiển thị

- **Mã phiếu**: 20261119-TC1 (phiếu con; phiếu tổng `~/projects/logs/handover/20261119-TC1_ten-goi-rieng-toan-he.md`)
- **Từ**: phiên tsufiles-3c, 19/11/2026, theo yêu cầu trực tiếp của chủ dự án
- **Đến**: phiên tsufiles (phiên này hoặc kế tiếp)
- **Trạng thái**: MỞ

## 1. Nguồn gốc (đọc lại lịch sử 19/11)

- 16/11/2026 chủ dự án yêu cầu "bản cho smartphone iOS, iPhone 8 tìm thấy, gửi và nhận với máy tính và thiết bị khác".
  Yêu cầu vào phiên tsufiles ⇒ ADR-0004 (S1 máy chủ LAN, S2 trang web + QR, S3 tìm máy, S4 app iOS).
- 18/11 S3 (#105, #106) chọn **giao thức LocalSend gốc** để iPhone dùng app LocalSend có sẵn - không đối chiếu với Lotus
  Share 2.0 của hệ (ADR-0004 mục 4 còn ghi SAI rằng Lotus Share "chỉ đổi tiền tố API"; thực tế đổi cả cổng 53420 và
  nhóm `224.0.0.168`).
- 19/11 chủ dự án: có thể đã giao nhầm việc của lotus-share cho tsufiles; sản phẩm hệ chỉ dùng tên/giao thức của hệ.

Hiện trạng `main` (CHƯA phát hành - chưa người dùng nào cài): TsuFiles chiếm TCP + UDP 53317 (cổng LocalSend - đụng app
LocalSend cài cùng máy, đúng lỗi lotus-share từng gặp), giao diện 22 ngôn ngữ + `tsufiles help send` + CHANGELOG + docs
ghi "LocalSend".

## 2. Việc A - Trước bản phát hành kế tiếp (không chờ ai, làm ngay)

Mục tiêu: bản phát hành tới không mang chữ hay cổng LocalSend.

1. Tắt LocalSend ở mọi nơi bật nó: `PhoneShareService.Start` và `ShareCommand` bỏ `LocalSend = new LocalSendOptions…`;
   ẩn mục "Thiết bị gần đây" trên trang chia sẻ; lệnh `send` gỡ khỏi `CliOptions.Commands`/Help. GIỮ mã lõi
   `Core/Share/LocalSend/` + test (sẽ thành lõi Lotus ở việc B) - đổi tên thư mục/namespace sang trung tính
   (`Core/Share/Nearby/`, `NearbyProtocol`…) để không còn tên bên thứ ba trong mã.
2. Gỡ 11 khóa `share.nearby*`/`share.send*` khỏi 22 tệp i18n (hoặc đổi chữ "LocalSend" → "Lotus Share" nếu việc B làm
   cùng PR); bỏ đoạn LocalSend trong `docs/vi/CHIA-SE-DIEN-THOAI.md`, `KIEN-TRUC-BAO-MAT.md`, CHANGELOG khối `<!-- update -->`.
3. ADR-0004: thêm mục "5. Sửa hướng S3 (19/11/2026)" - ghi đúng sự thật (đã chọn LocalSend, vì sao sai, đổi sang Lotus
   Share); sửa câu sai về Lotus Share ở mục 4.
4. Đã có sẵn trong đợt này: bản dịch `de.json` còn 14 chuỗi "Filee" người dùng thấy (`startup.elevated_other`,
   `installer.*`, `theme.*`, `triggers.gnome_*`…) - `tools/xoa-ten-cu.py` bỏ sót vì khuôn `Filee-` (gạch nối + chữ);
   sửa tay + thêm ca vào `--kiem`. Website `/dinh-dang`: ví dụ lệnh `filee formats` là SAI (lệnh thật `tsufiles formats`).

## 3. Việc B - Nối Lotus Share (CHỜ lotus-share trả lời phiếu con của họ)

Phụ thuộc: `lotus-share/logs/handover/20261119-TC1_yeu-cau-ten-goi-va-hop-dong-giao-thuc.md` mục 2-3 (đặc tả
`docs/GIAO-THUC-LOTUS-V2.md`, mẫu kiểm, xác nhận `SO_REUSEADDR` UDP 53420).

1. Lõi `Core/Share/Nearby/`: cổng UDP 53420, nhóm `224.0.0.168` (+ `ff12::1075:5ea0` nếu đặc tả bắt buộc), tiền tố
   `/api/lotus/v2/`, HTTPS + ghim vân tay theo đặc tả. Máy chủ TsuFiles KHÔNG chiếm TCP 53420 (khai cổng riêng trong gói).
2. CI: test đối chiếu với `docs/giao-thuc-mau/` của lotus-share (chép kèm SHA-256 nguồn).
3. Thử thật: TsuFiles ↔ Lotus Share (Windows + Android) hai chiều; cài cả hai trên một máy.
4. Giao diện: "Thiết bị Lotus Share gần đây", hướng dẫn cài Lotus Share (lotus.tsudev.com), không nhắc app khác.

## 4. 👤 Câu hỏi cho chủ dự án (ghi câu trả lời vào đây)

- **S1/S2 "Chia sẻ với điện thoại" (trình duyệt + QR) cũng sinh ra từ yêu cầu 16/11.** Lotus Share đã có trang "chia sẻ
  qua web" + mã QR (`app/lib/pages/web_share_page.dart`). Giữ S1/S2 trong TsuFiles (điểm riêng: điện thoại gửi HEIC lên
  máy tính để chuyển đổi rồi tải lại JPG/PDF) hay chuyển hẳn mục tiêu "iPhone 8" sang lotus-share và gỡ khỏi TsuFiles?
  Agent đề xuất: GIỮ S1/S2 (gắn với chức năng chuyển đổi, không đụng cổng của Lotus Share - cổng ngẫu nhiên), còn tìm
  máy/gửi thẳng thì giao cho giao thức Lotus (việc B).
- **Ghi công tác giả gốc ngoài LICENSE**: README.md:11,116, README.en.md:12,80-81 ("Mọi công lao … thuộc về KnifeLemon/
  Filee"), SECURITY.md:17 ("báo tiếp lên upstream"), website `/ban-quyen` mô tả "ghi công tác giả KnifeLemon". Theo yêu cầu
  19/11 thì bỏ; trang `/ban-quyen` vẫn hiển thị NGUYÊN VĂN `LICENSE` (bắt buộc) nhưng bỏ đoạn giới thiệu tác giả gốc.
  `App.axaml` `Name="Filee"` (tên app ở menu macOS) là định danh lúc chạy? - kiểm ADR-0003 mục 5 trước khi đổi.

## 5. Nghiệm thu

- `python3 -I ~/projects/logs/tools/ra_ten_cu.py --dong tsufiles`: 0 dòng "LocalSend"; "Filee" chỉ còn ở chỗ thuộc mục 2
  phiếu tổng (LICENSE, THIRD-PARTY-NOTICES, định danh di trú ADR-0003) - liệt kê lý do từng dòng.
- `python3 -I tools/xoa-ten-cu.py --kiem` và `tools/doi-ten-tsudev.py --kiem` sạch; CI xanh.
