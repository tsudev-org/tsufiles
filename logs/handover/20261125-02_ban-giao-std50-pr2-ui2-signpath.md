# PHIẾU BÀN GIAO 20261125-02 - STD50 PR 2, UI2 phần A, SEC2 E8 chuẩn bị (phiên tsufiles-c4)

- **Ngày**: 25/11/2026 - **Phiên**: tsufiles-c4 (AKB `SES-20261125-0923-cecc`)
- **PR**: #126 `feat/std5-cong-cu-quy-dao` (gộp `391c97e9`), #127 `feat/sec2-code-signing-policy` (gộp `305347a2`)
- **Website**: deploy version `22a79ee0`; production rà 12 trang x 3 khổ x Sáng/Tối sạch, og-image khớp SHA-256 bản build

## 1. Đã xong

| Phiếu | Kết quả |
| --- | --- |
| STD50 | ĐÓNG. PR 2 (#126): trang chủ bố cục `cong-cu` (khối thao tác căn giữa: ví dụ chuyển đổi, nút Tải cho Windows + dung lượng, liên kết hệ khác, lệnh Scoop có nút Chép), hoạ tiết `quy-dao` tĩnh ở hero, og-image có vòng quỹ đạo (màu đọc từ `products/files.json`). Ảnh trước/sau: `docs/anh-chup/20261125-std50-pr2/`. App: màn đầu đã là vùng thả tệp lớn căn giữa - không đổi. |
| UI2 | Phần A xong (12 trang x 360/768/1366 x Sáng/Tối + quay khung hình tải lạnh): sửa tràn ngang `/ban-quyen/` khổ 360, sửa xung đột hai trình chép `[data-chep]` ở `theme.js`, font dự phòng Be Vietnam Pro đo thật (mục 0.2). Phần B: chờ bảng màu mới của tsudev-standards. Phiếu còn MỞ. |
| SEC2 E8 | Đo ngưỡng SignPath 1/6 ⇒ KHÔNG nộp. Đã chuẩn bị (#127): trang `/code-signing-policy/`, `tools/signpath/don.json` + `nop-don.mjs`. |

## 2. Việc tiếp theo (theo thứ tự)

1. **UI2 phần B** khi tsudev-standards phát hành bảng màu Sáng dịu (UX2-b/c của tsudev): sync `.standards/`, chụp lại
   3 khổ x Sáng/Tối, đóng phiếu UI2. Có thể đề xuất ở tsudev-standards thêm font dự phòng cho `--font-display`
   (Lexend 600 so Arial Bold: size-adjust 105,1%).
2. **PRO1/PRO2 + QR4**: chờ thư viện .NET `TsuSync` (PRO-D3). Làm được ngay: kiểm kê dữ liệu rời máy + ADR.
3. **SignPath**: từ **28/11/2026** đo lại ngưỡng theo mục 5 dưới đây; đạt >= 4/6 thì chuẩn bị để 👤 nộp.
4. Lặt vặt: chạy `tools/thuong-hieu/tao-tai-san.py` làm thay đổi `src/TsuFiles.App/Assets/Icons/tsufiles.ico` (không
   cố ý, đã hoàn lại) - xem có phải script ghi ICO không tất định không.

## 3. Việc của chủ dự án (👤)

- Còn từ phiếu 20261125-01: secret `VT_API_KEY`, **đổi khóa VirusTotal** đã lộ.
- SignPath (khi đạt >= 4/6): `cd website && node ../tools/signpath/nop-don.mjs --headed` (xem ảnh) rồi `--submit`,
  xác nhận email. Người nộp đọc từ `~/.config/tsudev/signpath-applicant.json`.
- `VT_API_KEY` là secret của **GitHub Actions** (job `virustotal` trong `.github/workflows/release.yml` gọi workflow
  chung `tsudev-org/.github`), KHÔNG phải biến môi trường Render: tsufiles không có dịch vụ nào trên Render (website
  chạy Cloudflare Workers). Lấy khóa: virustotal.com -> đăng nhập -> ảnh đại diện -> API key (khóa cũ đã lộ: tạo khóa
  mới trước). Đặt: `! gh secret set VT_API_KEY -R tsudev-org/tsufiles` (nhập ẩn, khóa không đi qua phiên agent), hoặc
  GitHub -> Settings -> Secrets and variables -> Actions. Lưu bản máy ở `~/.config/tsudev/virustotal.env` dạng
  `VT_API_KEY=...` (KHÔNG `source` tệp khóa thô - AKB `ISS-20261125-0200-8ae9`).

## 4. Bài học (AKB)

- `LES-20261125-0959-5522`: font dự phòng - `local()` cần tên đầy đủ/PostScript; đo bằng fontTools + Arimo; kiểm bằng
  fontconfig tạm + CDP `CSS.getPlatformFontsForNode`.
- `LES-20261125-0959-4d6f`: Pillow paste RGBA qua mặt nạ từ chính alpha ⇒ alpha nhân hai lần.
- Cổng build `/security/`: `trust.json` chỉ được liên kết tới bên thứ ba (không trỏ về chính website).

## 5. Kiểm ngưỡng SignPath - phiên đầu tiên từ 28/11/2026 trở đi

Bối cảnh: 25/11/2026 đo được **1/6** (chỉ đạt mục 5). Ngưỡng ở `~/projects/logs/handover/20261113-02_ke-hoach-uy-tin-he-sinh-thai.md`
mục 5. Agent KHÔNG tự nộp, KHÔNG tạo tài khoản, KHÔNG nhờ người "star hộ" (mục 0 của phiếu đó).

1. Đo số liệu toàn hệ: `cd ~/projects/tsudev && npm run he-sinh-thai:tin-hieu` (chỉ đọc; nếu phiên tsudev đang chạy thì
   nhắn phiên đó thay vì commit hộ `logs/tin-hieu/`).
2. Đo riêng tsufiles, ghi từng mục ĐẠT/CHƯA kèm số:

   | # | Ngưỡng | Cách đo | 25/11/2026 |
   | --- | --- | --- | --- |
   | 1 | >= 100 sao + có fork của người ngoài | `gh api repos/tsudev-org/tsufiles --jq '.stargazers_count,.forks_count'`; `gh api repos/tsudev-org/tsufiles/forks --jq '.[].owner.login'` | 0 sao, 0 fork - CHƯA |
   | 2 | >= 3 người đóng góp ngoài 12 tháng (PR đã gộp) | `gh pr list -R tsudev-org/tsufiles --state merged --limit 200 --json author --jq '[.[].author.login]\|unique'`, trừ `tsudev-tsudev`, `nguyentrangtinhsu`, `b4djl1h`, bot. KHÔNG đếm tác giả commit upstream Filee | 0 - CHƯA |
   | 3 | >= 2 bài/video độc lập hoặc thảo luận >= 20 bình luận | tìm "TsuFiles" trên web/Reddit/HN/YouTube; chỉ tính bài người khác viết | 0 - CHƯA |
   | 4 | >= 6 tháng phát hành đều (>= 1 bản/tháng), ghi chú tiếng Anh | `gh release list -R tsudev-org/tsufiles` | 2 bản trong 2 tuần - CHƯA (sớm nhất 05/2027) |
   | 5 | Scorecard >= 7 + Best Practices passing | `curl -s https://api.securityscorecards.dev/projects/github.com/tsudev-org/tsufiles \| jq .score`; bestpractices.dev/projects/16719 | 7.2 + passing - ĐẠT |
   | 6 | Lượt tải tăng đều | tổng `download_count` mọi bản (`gh api repos/tsudev-org/tsufiles/releases --jq '[.[].assets[].download_count]\|add'`), so với lần đo trước | 1.160 - chưa đủ chuỗi |

3. Ghi kết quả: một dòng vào mục "Kết quả xử lý" của phiếu `20261125-SEC2` + mục 7 của phiếu toàn hệ `20261113-02`
   (nếu phiên tsudev không giữ khóa tệp đó; nếu có thì nhắn phiên tsudev).
4. **< 4/6**: không làm gì thêm với SignPath; hẹn lần đo sau (đầu tháng kế tiếp) trong STATE.
5. **>= 4/6**:
   - Kiểm `docs/CODE_SIGNING_POLICY.md` còn đúng sự thật (workflow, tên tệp phát hành, mục quyền riêng tư, bộ cài);
     đối chiếu lại điều kiện ở signpath.org/terms (có thể đã đổi).
   - Kiểm `tools/signpath/don.json` (mô tả, kênh phân phối - thêm winget nếu đã có) và biểu mẫu HubSpot chưa đổi trường
     (đọc trang bằng Playwright, KHÔNG điền họ tên/email).
   - Phiếu toàn hệ chỉ nộp cho MỘT dự án tại một thời điểm: hỏi phiên tsudev xem dự án nào đang nộp.
   - Báo 👤 chạy `cd website && node ../tools/signpath/nop-don.mjs --headed`, xem ảnh, rồi `--submit`, xác nhận email.
   - Sau khi SignPath duyệt: tích hợp ký trong CI (`docs/vi/PHAT-HANH.md` mục 4), đổi mục "Signing provider" của chính
     sách sang câu ghi công, cập nhật `trust.json` E8 (liên kết bên thứ ba), câu SmartScreen ở `/tai-ve/`.

