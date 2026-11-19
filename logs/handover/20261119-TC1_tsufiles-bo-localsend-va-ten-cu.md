# PHIẾU YÊU CẦU - TC1 (tsufiles): rút giao thức LocalSend, nối Lotus Share; bỏ tên cũ còn hiển thị

- **Mã phiếu**: 20261119-TC1 (phiếu con; phiếu tổng `~/projects/logs/handover/20261119-TC1_ten-goi-rieng-toan-he.md`)
- **Từ**: phiên tsufiles-3c, 19/11/2026, theo yêu cầu trực tiếp của chủ dự án
- **Đến**: phiên tsufiles (phiên này hoặc kế tiếp)
- **Trạng thái**: MỞ - việc A XONG (#111, chờ CI/gộp); việc B SẴN SÀNG làm (lotus-share đã trả lời 19/11)

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

## 2. Việc A - XONG 19/11/2026 (PR #111)

Đã làm: revert #106 + #105 (thay cho tắt/đổi tên - mã đó nói giao thức của app ngoài hệ); README/website/
CONTRIBUTING/SECURITY/i18n ja-ko-de/tài liệu kỹ thuật; `tools/xoa-ten-cu.py` bắt được chữ Hán/Kana/Hangul. Kế hoạch ban
đầu giữ để đối chiếu:

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

## 3. Việc B - "Gửi bằng Lotus Share" (giao tệp, KHÔNG nói giao thức) - LÀM 19/11/2026

**Đổi hướng 19/11/2026** (chủ dự án giao agent chọn phương án tối ưu, không xung đột giữa các dự án): BỎ kế hoạch tự cài
giao thức Lotus v2 trong TsuFiles (B1-B6 cũ: chứng thư + mTLS, UDP 53420, máy chủ HTTPS, client). Lý do và so sánh:
ADR-0004 mục 5 (dư thừa với Lotus Share; hai mục cho cùng một máy; tranh UDP 53420; thêm máy chủ luôn nghe cho app
ngoại tuyến D3; buộc lịch phát hành hai bên). Mã #105/#106 KHÔNG dùng lại.

Làm thay (một PR):
1. `Core/Share/LotusShare.cs`: tìm Lotus Share ở chỗ bộ cài chính thức đặt (Windows `InstallLocation` của AppId
   `{21D09887-EE8E-478B-B43F-BD492DE6EFE3}_is1` → `lotus_share.exe`; Linux `/opt/lotus-share/lotus_share`,
   `/usr/bin/lotus-share` (đo từ gói deb 26.11.1801); macOS `Lotus Share.app`), không dò PATH; mở kèm đường dẫn tuyệt
   đối qua `ArgumentList` (macOS `open -b com.tsudev.lotusshare`). Test `LotusShareTests`.
2. Thẻ kết quả trang chủ: nút chia sẻ `home.send_lotus` (22 ngôn ngữ), chỉ hiện khi đã cài Lotus Share.
3. ADR-0004 mục 2.3 + 5, `docs/vi/CHIA-SE-DIEN-THOAI.md`, CHANGELOG.
4. ĐÃ TRẢ LỜI: lotus-share#85 - hợp đồng `docs/MO-KEM-TEP.md` + test khóa `load_selection_from_args_test.dart`;
   đặc tả giao thức đã bỏ tên TsuFiles. Thử thật dùng Lotus Share **26.11.1901** trở lên (26.11.1801 không mở được).
   Phiếu gốc: TsuFiles KHÔNG còn là client giao thức (bỏ các dòng nhắc TsuFiles trong đặc tả); hợp đồng
   duy nhất là "tệp chạy nhận đường dẫn tệp/thư mục ở dòng lệnh" (`LoadSelectionFromArgsAction`, chuyển cho bản đang
   chạy qua `notifyRunningInstance`) + vị trí cài ở trên - đổi thì báo tsufiles.
5. 👤 Thử thật: Windows cài cả hai, bấm nút ở thẻ kết quả → Lotus Share mở với tệp; Lotus Share đang chạy sẵn → tệp vào
   cửa sổ đang mở.

## 4. Câu hỏi cho chủ dự án - ĐÃ CHỐT 19/11/2026

Chủ dự án giao agent tự chọn phương án tối ưu (19/11). Agent chốt: (1) GIỮ S1/S2 trong TsuFiles (gắn với chuyển đổi,
cổng ngẫu nhiên, không đụng Lotus Share); (2) BỎ mọi lời ghi công tác giả gốc ngoài LICENSE/THIRD-PARTY-NOTICES (đã làm
ở #111). `App.axaml` `Name` đã đổi (chỉ menu macOS; WM_CLASS tường minh). Nội dung câu hỏi ban đầu:

- **S1/S2 "Chia sẻ với điện thoại" (trình duyệt + QR) cũng sinh ra từ yêu cầu 16/11.** Lotus Share đã có trang "chia sẻ
  qua web" + mã QR (`app/lib/pages/web_share_page.dart`). Giữ S1/S2 trong TsuFiles (điểm riêng: điện thoại gửi HEIC lên
  máy tính để chuyển đổi rồi tải lại JPG/PDF) hay chuyển hẳn mục tiêu "iPhone 8" sang lotus-share và gỡ khỏi TsuFiles?
  Agent đề xuất: GIỮ S1/S2 (gắn với chức năng chuyển đổi, không đụng cổng của Lotus Share - cổng ngẫu nhiên), còn tìm
  máy/gửi thẳng thì giao cho giao thức Lotus (việc B).
- **Ghi công tác giả gốc ngoài LICENSE**: README.md:11,116, README.en.md:12,80-81 ("Mọi công lao … thuộc về KnifeLemon/
  Filee"), SECURITY.md:17 ("báo tiếp lên upstream"), website `/ban-quyen` mô tả "ghi công tác giả KnifeLemon". Theo yêu cầu
  19/11 thì bỏ; trang `/ban-quyen` vẫn hiển thị NGUYÊN VĂN `LICENSE` (bắt buộc) nhưng bỏ đoạn giới thiệu tác giả gốc.
  `App.axaml` `Name="Filee"` (tên app ở menu macOS) là định danh lúc chạy? - kiểm ADR-0003 mục 5 trước khi đổi.

## 4b. Tin từ phiên điều phối (projects-19, 19/11/2026)

- Phiếu con tsudev bước 1 (bỏ `basedOn`, dòng "Dựa trên", ô tìm `/apps`): tsudev-org/tsudev#315, chờ `nguyentrangtinhsu`
  duyệt (chạm cổng bản quyền). Bước 2-5 tsudev + phiếu con trafficmonitor: phase T0/TC1 của
  `~/projects/logs/handover/20261119-01_ke-hoach-toan-he-v3.8.0.md`.
- tsudev-standards **v3.8.0** đã phát hành (tên hiển thị TsuDEV, `PRODUCT_POLICY.md`, `STORE_SUBMISSION.md`): làm việc A
  cùng đợt với `./scripts/sync-standards.sh --ref v3.8.0` để đổi chữ một lần - đọc phiếu `20261119-01` hàng tsufiles trước.

## 5. Nghiệm thu

- `python3 -I ~/projects/logs/tools/ra_ten_cu.py --dong tsufiles`: 0 dòng "LocalSend"; "Filee" chỉ còn ở chỗ thuộc mục 2
  phiếu tổng (LICENSE, THIRD-PARTY-NOTICES, định danh di trú ADR-0003) - liệt kê lý do từng dòng.
- `python3 -I tools/xoa-ten-cu.py --kiem` và `tools/doi-ten-tsudev.py --kiem` sạch; CI xanh.
