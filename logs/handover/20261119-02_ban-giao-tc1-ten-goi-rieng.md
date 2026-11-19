# PHIẾU BÀN GIAO - TC1 tên gọi riêng (việc A), chuẩn v3.8.0, kế hoạch nối Lotus Share

- **Mã phiếu**: 20261119-02
- **Từ**: phiên tsufiles-3c, 19/11/2026 (tiếp nối `20261118-03`)
- **Trạng thái**: MỞ (việc tiếp theo ở mục 2)

## 1. Đã làm

| PR | Nội dung | Trạng thái lúc đóng phiên |
| --- | --- | --- |
| #109 | Phiếu con TC1 của tsufiles + STATE | ĐÃ GỘP |
| #110 | Đồng bộ bộ quy ước v3.7.0→**v3.8.1** (phiếu `20261119-T1`) (phase T1 phiếu `~/projects/logs/handover/20261119-01_*`) | ĐÃ GỘP 19/11 |
| #111 | **TC1 việc A**: revert S3 (#106, #105 - giao thức app ngoài hệ); bỏ tên dự án gốc khỏi README, website, CONTRIBUTING, SECURITY, i18n ja/ko/de, tài liệu kỹ thuật; sửa các dòng ghi SAI sau đợt đổi tên (`filee formats`, `filee-cli`, `./Filee`, thư mục engine) | ĐÃ GỘP 19/11 (2 job đỏ không liên quan - chạy lại xanh) |
| #113 | CI: nâng giới hạn job WebKit (`share-web.yml`) 25 → 45 phút (24m17s ở #104, hết giờ ở #111) | ĐÃ GỘP 19/11 |

Ngoài repo:
- Phiếu toàn hệ `~/projects/logs/handover/20261119-TC1_ten-goi-rieng-toan-he.md` + công cụ dùng chung
  `~/projects/logs/tools/ra_ten_cu.py` (rà 12 repo, xếp loại chỗ xuất hiện; `--dong <repo>` in từng dòng).
- Phiếu con: lotus-share (ĐÃ TRẢ LỜI - đặc tả `docs/GIAO-THUC-LOTUS-V2.md`, lotus-share#75 đã gộp), tsudev (bước 1 ở
  tsudev#315 chờ 👤 duyệt), tsudev-trafficmonitor (chờ 👤 chọn tên sản phẩm). Phiên điều phối projects-19 đã nhận.

Bài học:
- `\b`/`\w` của regex coi chữ Hán/Kana/Hangul/Thái là chữ ⇒ "Fileeを開く", "Filee를" lọt mọi công cụ dò tên (181 chuỗi
  ja/ko). Dò tên trong văn bản đa ngữ: dùng ranh giới chữ LATINH `(?<![A-Za-z0-9])…(?![A-Za-z0-9])`.
- Giao diện vốn hiện "TsuFiles" nhờ `ForkDefaults.Brand()` đổi lúc chạy (bản dịch, Help CLI, Setup) - tên cũ chỉ còn
  trong tệp nguồn công khai. Đọc hàm này trước khi kết luận "người dùng thấy tên cũ".
- Rút một tính năng đã gộp mà chưa phát hành: `git revert -m 1 <merge>` sạch hơn tắt/đổi tên; ghi SHA để lấy lại phần
  dùng chung (ADR-0004 mục 4).
- Định danh lúc chạy (mutex `Filee-<hash>`, thư mục `~/.local/share/filee/`, khóa registry) KHÔNG đổi theo đợt bỏ tên:
  công cụ `xoa-ten-cu.py` coi `Filee-<chữ thường>` là định danh, `Filee-<Danh từ viết hoa>` (tiếng Đức) là chữ hiển thị.

## 2. Việc tiếp theo (theo thứ tự)

1. ~~Gộp #110, #111, #113, #112; deploy website~~ XONG 19/11: chủ dự án deploy (Worker version
   `fe26cf1a-6ff0-4a72-938e-604ca4afeca8`); quét production 5 trang × 3 khung 200 OK; SHA-256 4 trang đổi khớp `dist/`;
   0 chữ LocalSend/upstream/`filee formats`; "KnifeLemon" chỉ còn trong ô LICENSE nguyên văn `/ban-quyen` (bắt buộc MIT).
2. **TC1 việc B** - nối giao thức Lotus v2: kế hoạch B1-B6 ở `logs/handover/20261119-TC1_tsufiles-bo-localsend-va-ten-cu.md`
   mục 3 (lotus-share#75 ĐÃ GỘP, `main` `2d392aeb`). Bắt buộc mTLS, vân tay hex HOA, không chiếm TCP 53420.
3. Phase của phiếu toàn hệ `20261119-01` áp cho tsufiles (đọc hàng tsufiles trước):
   - ~~**T2**~~ XONG 19/11: #115 đã gộp (TsuDEV hiển thị + website bảng xanh 4.2; phiếu
     `~/projects/logs/handover/20261119-T2_*` mục 4; website deploy version `d58d4276`). Theme app "Cú đêm" GIỮ TÍM (màu logo con cú) - chủ dự án chốt
     19/11/2026, ngoại lệ như tím hoa sen Lotus Share (báo qua phiên điều phối projects-d7; ghi vào ECOSYSTEM_IDENTITY 4.2).
     KHÔNG đổi `NightOwlPalette` sang xanh.
     **Sau đó chủ dự án chốt thêm (19/11, phiếu `~/projects/logs/handover/20261119-TF1_*`): website files.tsudev.com cũng
     tím logo cú** (ECOSYSTEM_IDENTITY 4.2.1, chuẩn v3.8.3) - đảo phần website của #115; chữ TsuDEV giữ nguyên.
   - **T3**: PRODUCT_POLICY mục 1 - lần chạy đầu ngôn ngữ + giao diện **theo hệ thống** (hiện TsuFiles ÉP tiếng Việt:
     ngược quy ước). Sửa cùng lúc mã, README ("Tiếng Việt mặc định" ở bảng Điểm nổi bật), website, 3 test bắt buộc
     (hệ thống `vi`, `en`, ngôn ngữ chưa hỗ trợ ⇒ tiếng Anh; hệ thống sáng/tối).
4. V1 bước 5 (khuôn phiên bản `26.M.DDNN`): vẫn CHỜ tsucwico/tsuswico/trafficmonitor; test #107 khóa hợp đồng.
5. 👤: S4 iOS (đầu 2027 theo phiếu 20261119-01), MSIX/Store, ký mã, notarize.

## 3. Ghi chú môi trường

- Website: `cd website && npm run build` có `check-dist` (mô tả trang 120-160 ký tự).
- `python3 -I ~/projects/logs/tools/ra_ten_cu.py --dong tsufiles` sau mỗi đợt sửa chữ; dòng còn lại phải thuộc ngoại lệ
  mục 2 phiếu tổng TC1.
