# PHIẾU YÊU CẦU SEC1 - Nhận token/secret qua clipboard (tsufiles)

- **Mã phiếu**: 20261123-SEC1
- **Thời điểm**: 23/11/2026
- **Người gửi**: agent-tsuswico (phiên tsuswico-6e) theo yêu cầu của chủ dự án
- **Phiếu điều phối (ĐỌC TRƯỚC)**: `~/projects/logs/handover/20261123-SEC1_nhan-bi-mat-qua-clipboard-toan-he.md`
- **Trạng thái**: ĐÓNG (25/11/2026, phiên tsufiles-e9)

## 1. Quy tắc áp dụng ngay cho agent của repo này

- KHÔNG đề nghị chủ dự án dán token/secret vào chat, tệp hay lệnh `!`. Lỡ dán vào chat ⇒ coi là đã lộ, đề nghị thay.
- Nhận bí mật: chạy `tsu-nhan-bi-mat ...` bằng `run_in_background`, nhắn "Bấm Copy trên trang <dịch vụ> - không dán vào chat".
  Ưu tiên `--stdout | <lệnh đích>`; cần giữ ⇒ `--luu ~/.config/tsudev/<ten>.env` (chmod 600, ngoài repo).
- KHÔNG in giá trị bí mật (`cat .env`, `sed`…); chỉ in tên biến/độ dài/kết quả kiểm tra.
- Cần người dán vào dashboard ⇒ `tsu-nhan-bi-mat --dua-vao-clipboard <tệp>` rồi `--xoa-lich-su <tệp>`.
- Lịch sử Win+V trên máy đang BẬT ⇒ luôn để công cụ xoá mục khỏi lịch sử.

## 2. Việc cần làm

1. Rà tài liệu/hướng dẫn của repo (README thiết lập, `docs/`, `logs/STATE.md`, phiếu cũ còn mở) - chỗ nào bảo dán token
   vào chat/.env bằng tay ⇒ sửa theo phiếu điều phối mục 2-3.
2. Kiểm `.gitignore` có `.env*` (trừ `.env.example`); gitleaks/push protection theo SECURITY_BASELINE.
3. Liệt kê TÊN các bí mật repo dùng + nơi lưu (GitHub Actions secret / Cloudflare / Vercel / Render / `~/.config/tsudev/`)
   + phạm vi quyền vào mục 3 dưới (không ghi giá trị). Cái nào từng lộ trong chat/log ⇒ báo chủ dự án thay.
4. Ghi AKB của repo "áp dụng SEC1", đổi Trạng thái → ĐÓNG.

## 3. Bí mật của repo (agent điền - CHỈ TÊN)

| Tên | Nơi lưu | Phạm vi | Ghi chú |
| --- | --- | --- | --- |
| `GITHUB_TOKEN` | Tự cấp mỗi lần chạy GitHub Actions | `permissions:` khai từng workflow (mặc định `contents: read`) | Repo KHÔNG có Actions secret/variable nào khác (`gh secret list` rỗng 25/11/2026) |
| Khóa ký cập nhật (ECDSA P-256) | `~/.config/tsudev-files/keys/update-key.pem` (600, ngoài mọi working tree) | Ký `*SHA256SUMS*.txt` (`tools/sign-checksums-vn.sh`) | Khóa công khai ghim trong `ForkDefaults.cs`; quy trình `docs/vi/PHAT-HANH.md`; chưa từng lộ |
| Phiên Cloudflare của `wrangler login` | Hồ sơ OAuth của wrangler trên máy chủ dự án | Deploy Worker `tsufiles` (files.tsudev.com) | Không có API token lưu cho repo này |
| `VT_API_KEY` | `~/.config/tsudev/virustotal.env` (dùng chung toàn máy) | Đọc kết quả VirusTotal (phiếu AV1) | Chỉ nạp vào biến môi trường của tiến trình, không in |
| `WINGET_TOKEN` (chưa tạo) | Sẽ là GitHub Actions secret | PR vào `microsoft/winget-pkgs` | Việc 👤; nhận bằng `tsu-nhan-bi-mat --stdout \| gh secret set WINGET_TOKEN -R tsudev-org/tsufiles` |

## 4. Kết quả xử lý

- 25/11/2026 - tsufiles-e9: (1) rà README, `docs/`, `website/README.md`, `logs/STATE.md`, phiếu còn mở - không chỗ nào bảo
  dán token vào chat/tệp; deploy website dùng `wrangler login` (OAuth, không token). (2) `.gitignore` có `.env`, `.env.*`
  trừ `*.example`/`*.sample`; gitleaks chạy trong `security.yml` + push protection bật ở repo. (3) Bảng mục 3. Không bí
  mật nào của repo từng xuất hiện trong chat/log. (4) AKB `tsufiles` ghi "áp dụng SEC1" ở SES của phiên.

