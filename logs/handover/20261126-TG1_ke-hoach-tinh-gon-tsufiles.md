# PHIẾU TG1 - Kế hoạch tinh gọn TsuFiles (bỏ ngôn ngữ, engine, tính năng dư thừa)

- **Mã phiếu**: 20261126-TG1 - **Từ**: phiên `tsufiles-c9`, 26/11/2026 - **Trạng thái**: MỞ
- **Yêu cầu chủ dự án (26/11/2026)**: mục tiêu là bộ chuyển định dạng chuyên nghiệp đầy đủ cho văn phòng/công sở (đặc biệt UBND, hệ thống chính trị VN, cả chuyên viên địa chính); app quá nặng làm build/deploy chậm. Chuẩn hóa kế hoạch: chỉ giữ **tiếng Việt +
  tiếng Anh**, bỏ tính năng trùng lặp/xung đột/dễ lỗi khi chuyển định dạng; app viết bằng ngôn ngữ mạnh, chuyên nghiệp,
  xử lý/tải nhanh.

## 0. Số đo hiện trạng (26/11/2026, nhánh `main` + #130)

| Hạng mục | Số đo |
| --- | --- |
| Gói phát hành 2.4.1-vn.1 | Portable Windows 156 MB, Setup 134 MB, dmg 133-140 MB, tar.gz Linux 122-127 MB |
| CI trên `main` | ~79 phút một lần chạy (job `win-arm64` luôn đỏ chờ P1 #42) |
| Mã nguồn | 1 550 tệp; 39 engine trong `EngineRegistry`; Engines 82k dòng (HWP 16k, PDF 8,7k, Office 6,2k, Media 5,5k, Geo 4,3k, 3D 4,1k, Font 4k, Ebook 3,8k, CAD 3,7k, OFD 2,3k) |
| Bản dịch | 22 ngôn ngữ, 3,2 MB JSON; font Noto Devanagari/Bengali/Arabic/Thai đóng gói chỉ để hiển thị các ngôn ngữ đó |
| Ảnh "Có gì mới" kế thừa | `Assets/WhatsNew/2.0.0` + `2.3.0` = 5,2 MB (ghi chú phát hành của bản gốc) |
| Engine tải thêm | LibreOffice 1,27 GB, Calibre 663 MB, FFmpeg 272 MB, Pandoc 236 MB, Tesseract 168 MB, Whisper 157 MB, Ghostscript 31 MB, ExifTool 30 MB, Upscayl 9 MB |

## 1. Nguyên tắc

1. **Ngôn ngữ lập trình giữ C# / .NET 10 + Avalonia 12** (biên dịch AOT/ReadyToRun được, đa nền tảng, kiểu chặt).
   Viết lại bằng ngôn ngữ khác không làm app nhẹ hơn mà mất 82k dòng đã kiểm thử; tối ưu bằng cắt bỏ + trimming.
2. **Mỗi cặp định dạng chỉ MỘT đường chuyển chính** (engine dựng sẵn ưu tiên, LibreOffice là dự phòng duy nhất cho
   văn phòng). Bỏ engine thứ hai làm cùng việc vì gây kết quả khác nhau tuỳ máy và tăng ma trận test.
3. **Microsoft Office được dùng khi máy Windows có Office** (👤 duyệt 26/11/2026, đảo quyết định settings v5 riêng
   cho Office): DOCX/XLSX/PPTX -> PDF qua chính Office (PR #133), Office lỗi/quá hạn thì nhường tuyến cũ (`IGivesWay`).
   Ngoài Office vẫn không dùng phần mềm cài sẵn.
4. Tương thích ngược: không đổi khóa JSON đầu ra CLI, không đổi tên tệp phát hành/updater; preset/cài đặt đã lưu trỏ
   tới engine/định dạng bị bỏ ⇒ di trú cài đặt (schema mới) + thông báo rõ thay vì lỗi.
5. Mỗi giai đoạn một PR, một phiên agent; cổng: build + test lọc + `dotnet format` + `check_i18n_vi.py`.

## 2. Phân loại tính năng

| Nhóm | Engine / tính năng | Đề xuất | Lý do |
| --- | --- | --- | --- |
| Lõi - GIỮ | Ảnh (Magick), PDF (PDFsharp, PDFium, pdf-text/tables/slides), DOCX writer, OOXML, bảng tính dựng sẵn, Markdown dựng sẵn, nén/giải nén, Email, P7m (chữ ký số VN), dữ liệu (CSV/JSON/YAML/TOML), phụ đề, Metadata (ExifTool), LibreOffice (dự phòng văn phòng), FFmpeg (âm thanh/video), Tesseract (OCR), Chia sẻ điện thoại | Giữ | Nhu cầu văn phòng/hành chính VN |
| Trùng lặp - BỎ | Pandoc (trùng Markdown + DOCX writer dựng sẵn, 236 MB) | Bỏ | Hai kết quả khác nhau cho cùng cặp |
| Trùng lặp - BỎ | Calibre (trùng `ebook` dựng sẵn, 663 MB) | Bỏ (giữ EPUB dựng sẵn) | Engine nặng nhất sau LibreOffice |
| Trùng lặp - BỎ | WIC (Windows, trùng Magick); Vision (macOS, trùng Tesseract) | Bỏ | Kết quả khác theo hệ điều hành |
| Trùng lặp - BỎ | Unhwp (trùng rhwp) | Bỏ cùng nhóm HWP hoặc giữ rhwp | |
| Chuyên biệt nước ngoài | HWP/HWPX (Hàn Quốc, 16k dòng + rhwp/unhwp), OFD (Trung Quốc) | **BỎ** (👤 duyệt 26/11) | Không phổ biến ở VN |
| Địa chính / kỹ thuật | CAD (DXF/DWG), Bản đồ (Geo) | **GIỮ** (👤 26/11: chuyên viên địa chính UBND cần) | Mục tiêu: văn phòng/công sở, UBND, hệ thống chính trị VN |
| Kỹ thuật khác | Font, Ghostscript (EPS/PS), ISO | **BỎ** (👤 duyệt 26/11); kiểm trước xem PDF nén/PDF-A có phụ thuộc Ghostscript không | |
| Kỹ thuật khác | Mô hình 3D, Ảnh -> 3D, ICNS | Tạm GIỮ (👤 chưa chọn bỏ) - hỏi lại ở G3 | |
| AI cục bộ | Whisper (giọng nói -> chữ), Upscayl (phóng ảnh), Dịch tài liệu (Ollama) | **BỎ** (👤 duyệt 26/11) | Phụ thuộc phần mềm/mô hình ngoài, chậm, dễ lỗi |
| Ngôn ngữ | 20 bản dịch ngoài vi/en + font chữ Ấn/Bengal/Ả Rập/Thái + logic pt-PT/pt-BR, zh-Hans/Hant | Bỏ (yêu cầu 👤) | |
| Tài sản kế thừa | `Assets/WhatsNew/2.0.0`, `2.3.0` (5,2 MB) | Bỏ | Ghi chú phát hành của bản gốc |

## 3. Giai đoạn (mỗi dòng = 1 PR = 1 phiên)

| GĐ | Nội dung | Cổng/kiểm chứng | Trạng thái |
| :-: | --- | --- | --- |
| G0 | Gộp #130 (PDF -> Word giữ bố cục) | CI xanh | ✅ #130 `8040fdc0` |
| G1 | **Chỉ vi + en** (PR #131): xoá 20 `i18n/*.json`, `AppLanguages`, ngôn ngữ của Setup/CLI, font chữ không dùng, logic pt/zh; di trú cài đặt ngôn ngữ khác -> `auto` (vi nếu hệ thống tiếng Việt, còn lại en); bỏ `WhatsNew` kế thừa; sửa test + website/README | Build, test App/Setup/Cli lọc, `check_i18n_vi.py` | ✅ #131 `64896b08` |
| G2 | Bỏ engine trùng lặp: Pandoc, Calibre, WIC, Vision, Unhwp; `EngineDownloads`, `engines.json`, `fetch-*.ps1`, preset/EnginePriority di trú | Test Engines/Core lọc theo vùng sửa | ⏳ #135 |
| G3a | Bỏ HWP/HWPX + OFD (rhwp, unhwp, hwpx-writer, `Hwp/`, `Ofd/`) | Như G2 | ⬜ |
| G3b | Bỏ AI cục bộ (Whisper, Upscayl, Dịch) + Font, Ghostscript, ISO | Như G2 | ⬜ |
| G4 | CI gọn: bỏ test/fixture của phần đã xoá, tách job theo vùng đổi (`changes`), tắt `win-arm64` tới khi có P1 | Đo thời gian CI trước/sau (mục tiêu <= 35 phút) | ⬜ |
| G5 | Gói nhẹ: bật trimming có kiểm (ILLink, `TrimmerRootDescriptor` cho Avalonia), ReadyToRun chỉ assembly khởi động, nén | Đo gói (mục tiêu Setup <= 80 MB), smoke launch, thời gian mở app | ⬜ |
| G6 | Tài liệu: README VI/EN, website (trang tính năng/hướng dẫn), CHANGELOG 2.5.0, ADR-0005 tinh gọn | Website build + rà production sau deploy | ⬜ |

## 4. Rủi ro

- Người dùng đang dùng ngôn ngữ/engine bị bỏ: di trú tự động + ghi trong ghi chú phát hành 2.5.0.
- Trimming có thể làm hỏng phản chiếu (XAML, JSON): chỉ bật sau smoke test đủ nền tảng ở G5.
- CLI: lệnh/định dạng bị bỏ trả mã lỗi + thông báo, khóa JSON giữ nguyên.

## 5. Kết quả xử lý

- 26/11/2026 - tsufiles-c9: mở phiếu, đo hiện trạng; 👤 duyệt mục 2. G0 xong (#130). G1 PR #131. Ngoài kế hoạch theo
  yêu cầu 👤: #132 PDF scan -> Word giữ bố cục (ĐÃ GỘP), #133 engine Microsoft Office. Bàn giao `20261126-01`.
- 26/11/2026 - tsufiles-c10: #131 (G1) và #133 (Office) gộp. G2 PR #135 (schema 77). Định dạng markup chỉ Pandoc đọc giữ
  trong danh mục, preset MOBI/AZW3 giữ (gỡ ở G3/G4 cùng ảnh chụp schema 58). 👤: ưu tiên Windows 10/11 + Microsoft Office
  + tệp scan. Bàn giao `20261126-02`.
