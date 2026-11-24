# Sổ báo nhầm antivirus - TsuFiles

> Mẫu: `.standards/docs/templates/TRUST_TICKET.md` mục D; chuẩn `.standards/docs/DISTRIBUTION_TRUST.md` mục 3, 3.1.
> Phiếu: `logs/handover/20261122-AV1_bao-nham-antivirus.md`. Đo bằng API VirusTotal v3 (`/api/v3/files/<sha256>`, khóa ở
> `~/.config/tsudev/virustotal.env` - tệp khóa thô, KHÔNG `source`, xem AKB `ISS-20261125-0200-8ae9`).

## 1. Bản Latest `v2.4.1-vn.1`

| Tệp | SHA-256 | 22/11/2026 | 25/11/2026 (quét lại) |
| --- | --- | --- | --- |
| `tsudev-files-2.4.1-vn.1-osx-arm64.dmg` | `b404220289bcb5bddabefabb423a6232ce788f0a5bdce1316122bce1e0fa9127` | 1/74 Microsoft `Trojan:Script/Wacatac.C!ml` | **0/74** - cờ đã gỡ |
| `tsudev-files-2.4.1-vn.1-osx-x64.dmg` | `919caa346d47032bc8dd4dd1c61944c25b1c32540e3e1d365892ebdc8e1b261f` | 0/74 | 0/74 (kết quả 22/11) |
| `tsudev-files-2.4.1-vn.1-win-Portable.zip` | `3bd7d4e36d0db19f4be4fcd0b9e5ef30fe870b1b70fe085d028d332f18f310f0` | 0/74 | 0/74 (kết quả 22/11) |
| `tsudev-files-2.4.1-vn.1-win-Setup.exe` | `3b749a46fab5fd500633c9c9fa5f22fe68a393dd793ad8a22e893dd1aa266b6a` | 0/74 | 0/74 (kết quả 22/11) |

Gói `.tar.gz` (Linux, macOS) chưa từng được gửi lên VirusTotal (API trả `NotFoundError`).

## 2. Hồ sơ đã nộp

| Hãng | Kênh | Ngày nộp | Mã hồ sơ | Kết quả |
| --- | --- | --- | --- | --- |
| Microsoft (WDSI) | wdsi/filesubmission | - | - | Không cần nộp: quét lại 25/11/2026 02:02 đã 0/74 (nhận diện học máy `!ml` tự gỡ) |

## 3. Quy trình cho bản phát hành sau

1. `release.yml` tạo Release NHÁP → job `virustotal` (workflow dùng chung `tsudev-org/.github`, ghim SHA) quét mọi gói,
   ghi bảng vào notes; có cờ ⇒ giữ nháp + đỏ. Cần secret `VT_API_KEY` ở repo (chưa có ⇒ job ghi "bỏ qua").
2. Tệp Windows ký sau (`build/sign-release.ps1`) đổi SHA-256 ⇒ quét lại bằng tay (`vt` API hoặc tải lên giao diện web)
   trước khi công bố.
3. Có cờ: hồ sơ trong ≤ 48 giờ theo DISTRIBUTION_TRUST 3.1 (mô tả: "TsuFiles - open-source offline file converter
   (desktop app + command-line tool, .NET/Avalonia, MIT)"; chạy cục bộ, mạng chỉ dùng cho kiểm tra cập nhật và tải engine
   tùy chọn khi người dùng bấm). Thư soạn sẵn: `~/projects/logs/handover/20261122-AV1_bao-nham-antivirus-toan-he.md` 3.9.
4. Đo lại 3-7 ngày sau; hết cờ ⇒ ghi ngày gỡ vào bảng mục 1.
