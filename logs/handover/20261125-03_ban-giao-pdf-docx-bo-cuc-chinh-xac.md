# PHIẾU BÀN GIAO 20261125-03 - PDF sang Word giữ nguyên bố cục (phiên tsufiles-c8)

- **PR**: #130 `feat/pdf-docx-bo-cuc-chinh-xac` - CHƯA GỘP (CI đang chạy lúc đóng phiên; GitHub sự cố một phần 25/11 tối).
- **AKB**: LES-20261125-2316-8299 (kỹ thuật), -ab08 (Office COM từ WSL), -e1e4 (python -I).

## 1. Đã xong
- Chế độ `PdfWordLayout.Exact` (mặc định PDF -> DOCX): nền = PDF render với chữ ẩn (`PdfHiddenText`), chữ trong text
  box tuyệt đối + giãn ký tự; annotation phía trước. Preset (22 ngôn ngữ) + CLI `--word-layout exact|flowing`.
- Đo bằng Microsoft Word trên 18 PDF thật của `Downloads/test-tsufiles` (133 trang): số trang khớp 18/18, F1 0,998
  (cũ 0,816), lệch <= 0,25 pt. Test: Engines Pdf/Ocr/Model 484, Core 1322, Cli 106 đạt; format gate sạch.

## 2. Việc tiếp theo (theo thứ tự)
1. Xem CI #130 (`gh pr checks 130 -R tsudev-org/tsufiles`); đỏ thì sửa, xanh thì gộp. Không có thay đổi website.
2. **DOCX/XLSX -> PDF lệch nhiều so với Word/Excel** (đo cùng phiên, LibreOffice hệ thống làm engine):
   DOCX TB 0,923 (d05 "Đáp án NQTW4" 0,53; nhiều trang 0,55-0,58); XLSX TB rất thấp: x02 Excel 96 trang -> 4,
   x08 4491 -> 3033, x01/x03/x04/x09 0,3-0,5 (vùng in, ngắt trang, tỉ lệ khác Excel). Hướng đề xuất: engine
   "Microsoft Office" trên Windows (COM Word/Excel `ExportAsFixedFormat`, chỉ khi máy có Office, ưu tiên cao hơn
   LibreOffice, tách tiến trình như bài học ab08); khi không có Office thì rà vùng in/FitToPage của bộ in XLSX.
   Bộ đo: `C:\Users\devng\AppData\Local\Temp\tsft\` (`office.sh`, `run.sh`, `wordall.sh`, `word1.ps1`,
   `excel1.ps1`, mẫu ASCII `a/ od/ ox/` + `a-map.txt`, `o-map.txt`); script Python ở scratchpad phiên (mất khi xóa) -
   chép `sosanh.py`/`chong.py`/`lech.py` vào `tools/do-chat-luong/` nếu làm tiếp.
3. Excel COM trả rỗng cho x06, x08 (cần kiểm lại).
4. Các việc cũ: phiếu 20261125-02 mục 2 (UI2-B, PRO, SignPath từ 28/11), BQ1, PRO3 (phiếu mới chưa xử lý).

## 3. Lưu ý
- Tệp mẫu của chủ dự án là văn bản hành chính thật: KHÔNG commit, KHÔNG đưa lên dịch vụ ngoài.
- `dotnet publish -r win-x64` sửa `packages.lock.json`: hoàn lại trước khi commit.
