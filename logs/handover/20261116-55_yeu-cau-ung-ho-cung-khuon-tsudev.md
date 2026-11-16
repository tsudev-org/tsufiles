# PHIẾU YÊU CẦU - Trang Ủng hộ cùng khuôn tsudev.com, số 0 trơn, logo chung tsudev ở chân trang

- **Mã phiếu**: 20261116-55
- **Từ**: phiên tsudev 55 (agent-tsudev), 16/11/2026
- **Trạng thái**: ĐÓNG 16/11/2026 (PR website-ung-ho-cung-khuon; mục 5: GIỮ tím riêng của logo cú TsuFiles)
- **Lý do không sửa hộ**: repo đang có phiên `tsufiles-9e` làm việc (quy tắc `~/.claude/CLAUDE.md`).

## Việc cần làm (website/)

1. **Trang `/ung-ho/` cùng khuôn https://tsudev.com/ung-ho** - chép nguyên
   `components/KhoiUngHo.astro` từ PR tsudev-org/tsudev-trafficmonitor#68 (hoặc lotus-share#46), thay khối
   `<ul class="kenh-ung-ho">…</ul>` + đoạn cảnh báo bằng `<KhoiUngHo kenh={KENH} ten="TsuFiles" />`.
   Dữ liệu vẫn CHỈ từ `src/lib/ung-ho.ts` (không đụng `ung-ho.json`/ảnh QR - CODEOWNERS).
2. **Nút Chép**: thêm khối `ganNutChep()` cuối `public/theme.js` (chép từ trafficmonitor#68) - theme.js là script duy
   nhất, `check-dist` canh.
3. **Số 0 trơn**: chủ dự án yêu cầu font có số 0 KHÔNG có ký tự bên trong. JetBrains Mono / Consolas / Roboto Mono…
   đều có chấm/gạch (đo bằng fontTools). Tự host **Chivo Mono** (OFL, có subset vietnamese): sao
   `chivo-mono-{400,600}-*.woff2` + `OFL-ChivoMono.txt` + `src/styles/fonts-mono.css` từ trafficmonitor#68, import
   sau `fonts.css`, rồi `:root { --font-mono: 'Chivo Mono', 'Cascadia Code', ui-monospace, monospace; }`.
4. **Logo chung tsudev ở chân trang**: cạnh chữ "tsudev" đang là `/brand/logo-mark.png` (dấu TsuFiles). Thay bằng
   logo chính thức `logo-tsudev-{48,96}.{avif,webp}` + `logo-tsudev-256.png` (sao từ
   `~/projects/tsudev/apps/frontend-main/public/brand/`). Logo TsuFiles ở đầu trang GIỮ NGUYÊN.
5. Bảng màu: tsudev.com đổi sang **đen tuyền + xanh logo** (16/11/2026; `tokens/extensions.tsudev-web.json` của
   tsudev). TsuFiles có logo cú tím riêng ⇒ giữ màu riêng hay theo xanh là quyết định của phiên tsufiles/chủ dự án.

Kiểm: `npm run build` (check-dist 0), `check:a11y` 0 (nút Chép phải nằm TRONG `<dd>`, axe bắt `definition-list`),
ảnh chụp 2 chế độ × 2 khổ.
