# PHIẾU BÀN GIAO 20261126-01 - Tinh gọn G1, PDF scan sang Word, engine Microsoft Office (phiên tsufiles-c9)

- **PR**: #130 ĐÃ GỘP (`8040fdc0`, G0); #132 ĐÃ GỘP (`0aed94aa`, PDF scan -> Word); #131 G1 chỉ vi + en (CI chạy
  lại sau khi gộp main, lúc đóng phiên 16 đạt / 2 đang chạy); #133 engine Microsoft Office (đã sửa test trang Bộ xử lý
  ở `de7b35c5`, CI chạy lại); PR nhật ký này (nhánh `chore/logs-20261126-ban-giao`, tách từ nhánh #131).
- **Phiếu kế hoạch**: `logs/handover/20261126-TG1_ke-hoach-tinh-gon-tsufiles.md` (G0-G6).
- **Quyết định chủ dự án 26/11/2026**: (1) chỉ vi + en; (2) BỎ HWP/HWPX + OFD, AI cục bộ (Whisper, Upscayl, Dịch),
  Font, Ghostscript, ISO; GIỮ CAD + Bản đồ (chuyên viên địa chính UBND); 3D/Ảnh->3D/ICNS chưa chọn - hỏi lại ở G3;
  (3) DÙNG Word/Excel khi máy có Office (đảo nguyên tắc settings v5 riêng cho Office) - #133;
  (4) ủy quyền dùng `C:\Users\devng\Downloads\test-tsufiles` để thử thật (KHÔNG commit, KHÔNG đưa ra dịch vụ ngoài).

## 1. Đã xong
- #131 G1 (chưa gộp): bỏ 20 bản dịch + ~35 MB font nhúng, `.lproj` macOS chỉ en/vi, settings schema 76 (ngôn ngữ đã bỏ -> auto),
  Setup `/LANG` chỉ vi/en; test cập nhật (lần đẩy cuối `e88a97cd`).
- #132 (ĐÃ GỘP): OCR + bố cục exact = chữ sửa được đúng chỗ trên bản scan đã tô nét cũ; màu/cỡ/đậm/nghiêng ước từ bản scan
  (ngưỡng đo thật: nghiêng +0,12 so với trang, đậm >= 0,132 em); `ScanText.FixDigits`; `vie` = tessdata_best (ghim
  SHA-256); CLI `--ocr vie`. Word mở 5 PDF scan (92 trang): 92/92 trang, F1 0,932/0,995/0,992/0,986/0,989.
- #133 (chưa gộp): `MicrosoftOfficeConverter` + `IGivesWay` (Office lỗi/quá hạn 5 phút -> tuyến kế tiếp). 9 DOCX và 8 XLSX trùng
  100% bản Word/Excel tự xuất; x06 (Excel tự xuất >28 phút không xong) -> tuyến dựng sẵn 25 trang.
- Đã dọn: worktree `../tsufiles-ocr`, `../tsufiles-office` (nhánh vẫn trên origin); không còn monitor/lock.
- Bộ đo: `tools/do-chat-luong/sosanh.py` (trong #132); cách dựng xem bộ nhớ `windows-test-files`.

## 2. Việc tiếp theo (theo thứ tự)
1. Gộp theo thứ tự: **#131** (xanh thì gộp) -> **#133**: gộp `origin/main` vào `feat/office-engine` (bản dịch khác
   vi/en bị xóa ở #131 nên test khóa i18n mới xanh; CHANGELOG: bỏ dấu xung đột, giữ mọi dòng), chạy
   `dotnet format TsuFiles.slnx --verify-no-changes --severity warn` + `./scripts/check-standards.sh`, đẩy, xanh thì
   gộp -> PR nhật ký này (gộp main vào nếu xung đột STATE). Lệnh xem lỗi CI:
   `gh api repos/tsudev-org/tsufiles/actions/jobs/<id>/logs | grep -E "^\s+Failed |Error Message" -A3`.
2. TG1 G2 (bỏ engine trùng: Pandoc, Calibre, WIC, Vision, Unhwp) -> G3a (HWP/HWPX/OFD) -> G3b (AI cục bộ, Font,
   Ghostscript, ISO; kiểm PDF nén/PDF-A có dùng Ghostscript không) -> G4 CI gọn -> G5 trimming/gói nhẹ -> G6 tài liệu.
   Mỗi giai đoạn: di trú EnginePriority/preset (schema 77+), thông báo rõ cho preset trỏ engine đã bỏ.
3. OCR: mặc định `OcrLanguage` vẫn "eng" - đề xuất đổi mặc định "vie" cho bản Việt (test RenderTests ~1512 kiểm "eng").
   Chữ viết tay/con dấu bị OCR ra chữ rác: cân nhắc lấy độ tin cậy (hOCR/TSV) để bỏ từ tin cậy thấp.
4. Office: PowerPoint chưa đo (không có tệp mẫu); test chọn tuyến Office chỉ chạy trên CI Windows.
5. Việc cũ: phiếu 20261125-02 mục 2 (UI2-B, PRO, SignPath đo từ 28/11), BQ1, PRO3, INT1, SYN1 (đã commit bản sao ở #131).

## 3. Lưu ý
- `taskkill /IM EXCEL.EXE` bị chặn (đúng: có thể đóng Excel của chủ dự án) - chỉ kết thúc PID do mình mở.
- Worktree mới thiếu `engines/` (7-Zip) nên `Zip_to_iso`/`Archive_to_iso` đỏ cục bộ - không phải lỗi.
- Chạy `./scripts/check-standards.sh` trước khi đẩy (em-dash U+2014 trong mã bị cổng bắt).

## 4. Prompt gợi ý cho phiên kế tiếp
> Bạn là agent-tsufiles. Đọc theo thứ tự: `AGENTS.md`, `logs/STATE.md`, phiếu
> `logs/handover/20261126-01_ban-giao-tinh-gon-ocr-office.md`, phiếu kế hoạch
> `logs/handover/20261126-TG1_ke-hoach-tinh-gon-tsufiles.md`; chạy `akb start "<mục tiêu>"`. Làm ĐÚNG MỘT bước mỗi phiên:
> (1) gộp #131 -> #133 -> PR nhật ký như mục 2.1 phiếu bàn giao; còn thời gian thì (2) giai đoạn kế tiếp của TG1 (G2) thành
> một PR. Trước mỗi lần đẩy: `dotnet format TsuFiles.slnx --verify-no-changes --severity warn` và
> `./scripts/check-standards.sh`; test App/Setup chỉ chạy lọc cục bộ, để CI chạy đủ. Không commit/đưa ra ngoài tệp của
> `Downloads\test-tsufiles`; không kết thúc tiến trình Office theo tên. Cuối phiên: viết phiếu bàn giao mới, cập nhật
> STATE, tắt monitor, `akb close` + `akb sync --project tsufiles`.
