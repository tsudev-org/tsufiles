#!/usr/bin/env python3
"""TsuFiles: sinh MỌI biểu tượng/logo/favicon của repo từ một ảnh gốc duy nhất.

Ảnh gốc: assets/brand/source/tsufiles-1.webp (ảnh bìa TsuFiles; con cú cầm thư mục nằm trong OWL_BOX). Không sửa tay
tệp sinh ra; đổi logo thì thay ảnh gốc, cập nhật SOURCE_SHA256 (và OWL_BOX) rồi chạy lại script này và commit kết quả.
Hướng dẫn: assets/brand/README.md.

  python3 -m venv /tmp/brand && /tmp/brand/bin/pip install -r tools/thuong-hieu/requirements.txt
  /tmp/brand/bin/python -I tools/thuong-hieu/tao-tai-san.py          sinh lại toàn bộ
  /tmp/brand/bin/python -I tools/thuong-hieu/tao-tai-san.py --kiem   chỉ kiểm (thoát 1 nếu thiếu/sai cỡ/sai dạng lớp)
  python3 -I tools/thuong-hieu/tao-tai-san.py --sua-ico              ghi lại các tệp .ico đã có theo dạng đúng (chỉ
                                                                    cần Python chuẩn, không cần Pillow)

Định dạng ICO (16/11/2026, sửa lỗi icon trống trên thanh tác vụ Windows): lớp < 256 px là ảnh BMP 32 bit (DIB, có
mặt nạ AND), chỉ lớp 256 px là PNG - đúng như ICO gốc của ứng dụng. Pillow ghi MỌI lớp thành PNG; Avalonia trên
Windows không tạo được HICON từ các lớp PNG nhỏ đó nên cửa sổ không có icon (WM_GETICON = 0) và thanh tác vụ hiện ô
trống. --kiem bắt lỗi này.

Các bước:
1. Cắt OWL_BOX, ước lượng nền chuyển sắc bằng mặt phẳng khớp với mép khung, tách nền bằng color-to-alpha theo khoảng
   cách tới nền tại chỗ. Chỉ vùng NỐI với mép mới là nền, nên lòng trắng mắt (gần màu nền) vẫn đục. Bỏ mảnh lạc
   (chữ "T" cạnh con cú), giữ khối lớn nhất cùng quầng sáng.
2. Cắt sát, đặt vào khung vuông trong suốt (bản chính). Thu nhỏ bằng LANCZOS trên alpha nhân sẵn (không viền đen).
3. Cỡ <= 48 px: cắt sát hơn (bỏ quầng mờ) và làm nét nhẹ để hình còn đọc được ở 16 px.
4. Con cú trong ảnh bìa chỉ cao khoảng 160 px: cỡ lớn hơn bản chính được phóng LANCZOS + làm nét. Có ảnh con cú riêng
   độ phân giải cao (>= 1024 px, hoặc SVG) thì thay ảnh gốc để icon 256/512 nét hẳn.
"""
from __future__ import annotations

import hashlib
import io
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/brand/source/tsufiles-1.webp"   # ảnh bìa TsuFiles (1024x512), con cú ở góc trái
SOURCE_SHA256 = "33974eb00c9b72fb7899027bb155662697700f51fc95b743adf9eb32f77bccee"
OWL_BOX = (20, 20, 212, 240)                            # khung chứa con cú trong ảnh bìa (trái, trên, phải, dưới)

BRAND = ROOT / "assets/brand"
APP_ICONS = ROOT / "src/TsuFiles.App/Assets/Icons"
WEB = ROOT / "website/public"
WEB_FONTS = WEB / "fonts"

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]  # gồm đủ 16/24/32/48/64/128/256 (BRAND_ASSETS mục 6, 7)
SMALL = 48
WEB_DARK = (10, 10, 10)  # --bg-base của chế độ Tối (website/src/styles/palette.css), cũng là theme_color của manifest

# Tệp sinh ra -> (rộng, cao). --kiem đối chiếu đúng bảng này.
OUTPUTS: dict[Path, tuple[int, int] | None] = {
    BRAND / "tsufiles-mark.png": None,
    BRAND / "tsufiles-mark.webp": None,
    BRAND / "tsufiles-mark.avif": None,
    BRAND / "tsufiles-mark-512.png": (512, 512),
    APP_ICONS / "tsufiles.png": (512, 512),
    APP_ICONS / "tsufiles.ico": (256, 256),
    WEB / "favicon.ico": (256, 256),
    WEB / "favicon-16x16.png": (16, 16),
    WEB / "favicon-32x32.png": (32, 32),
    WEB / "favicon-96x96.png": (96, 96),
    WEB / "apple-touch-icon.png": (180, 180),
    WEB / "android-chrome-192x192.png": (192, 192),
    WEB / "android-chrome-512x512.png": (512, 512),
    WEB / "android-chrome-maskable-192x192.png": (192, 192),
    WEB / "android-chrome-maskable-512x512.png": (512, 512),
    WEB / "brand/tsufiles-mark.png": None,
    WEB / "brand/tsufiles-mark.webp": None,
    WEB / "brand/tsufiles-mark.avif": None,
    WEB / "og-image.png": (1200, 630),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png_size(data: bytes) -> tuple[int, int]:
    return struct.unpack(">II", data[16:24])


def ico_sizes(data: bytes) -> list[int]:
    _, kind, count = struct.unpack("<HHH", data[:6])
    if kind != 1:
        raise ValueError("không phải ICO")
    return sorted((data[6 + 16 * i] or 256) for i in range(count))


def ico_entries(data: bytes) -> list[tuple[int, bytes]]:
    """(cỡ, dữ liệu) của từng lớp ICO."""
    count = struct.unpack("<HHH", data[:6])[2]
    out = []
    for i in range(count):
        w, _, _, _, _, _, size, offset = struct.unpack("<BBBBHHII", data[6 + 16 * i:22 + 16 * i])
        out.append((w or 256, data[offset:offset + size]))
    return out


def ico_png_layers(data: bytes) -> list[int]:
    """Các lớp < 256 px được lưu dạng PNG (phải là BMP, xem đầu tệp)."""
    return [size for size, body in ico_entries(data) if size < 256 and body[:8] == b"\x89PNG\r\n\x1a\n"]


def bmp_layer(size: int, rgba: bytes) -> bytes:
    """Một lớp ICO dạng DIB 32 bit: BITMAPINFOHEADER (cao gấp đôi), điểm ảnh BGRA từ dưới lên, mặt nạ AND."""
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, size * size * 4, 0, 0, 0, 0)
    rows = []
    for y in range(size - 1, -1, -1):
        row = bytearray(rgba[y * size * 4:(y + 1) * size * 4])
        row[0::4], row[2::4] = row[2::4], row[0::4]     # RGBA -> BGRA
        rows.append(bytes(row))
    stride = ((size + 31) // 32) * 4                    # mặt nạ AND 1 bit/điểm, dòng làm tròn 4 byte
    mask = bytearray()
    for y in range(size - 1, -1, -1):
        bits = bytearray(stride)
        for x in range(size):
            if rgba[(y * size + x) * 4 + 3] == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return header + b"".join(rows) + bytes(mask)


def ico_bytes(layers: list[tuple[int, bytes, bytes]]) -> bytes:
    """ICO từ (cỡ, RGBA, PNG) từng lớp: lớp 256 px giữ PNG, lớp nhỏ hơn ghi BMP."""
    bodies = [(size, png if size >= 256 else bmp_layer(size, rgba)) for size, rgba, png in sorted(layers)]
    out = bytearray(struct.pack("<HHH", 0, 1, len(bodies)))
    offset = 6 + 16 * len(bodies)
    for size, body in bodies:
        out += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(body), offset)
        offset += len(body)
    for _, body in bodies:
        out += body
    return bytes(out)


def png_rgba(data: bytes) -> tuple[int, bytes]:
    """Giải mã PNG RGBA 8 bit không xen kẽ (lớp ICO do Pillow ghi) bằng thư viện chuẩn: (cỡ, RGBA)."""
    import zlib

    pos, idat, width = 8, b"", 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            if (depth, color, interlace) != (8, 6, 0) or width != height:
                raise ValueError("chỉ hỗ trợ PNG RGBA 8 bit vuông, không xen kẽ")
        elif kind == b"IDAT":
            idat += chunk
        pos += 12 + length
    raw, stride, out = zlib.decompress(idat), width * 4, bytearray()
    previous = bytearray(stride)
    for y in range(width):
        kind, line = raw[y * (stride + 1)], bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for x in range(stride):
            a = line[x - 4] if x >= 4 else 0
            b = previous[x]
            c = previous[x - 4] if x >= 4 else 0
            if kind == 1:
                line[x] = (line[x] + a) & 0xFF
            elif kind == 2:
                line[x] = (line[x] + b) & 0xFF
            elif kind == 3:
                line[x] = (line[x] + (a + b) // 2) & 0xFF
            elif kind == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 0xFF
        out += line
        previous = line
    return width, bytes(out)


def fix_icos() -> int:
    """--sua-ico: ghi lại .ico của ứng dụng có lớp nhỏ dạng PNG thành lớp BMP (giữ nguyên điểm ảnh). favicon của
    website giữ lớp PNG (trình duyệt đọc được, tệp nhỏ hơn)."""
    for path in OUTPUTS:
        if path.suffix != ".ico" or path.parent != APP_ICONS or not path.is_file():
            continue
        data = path.read_bytes()
        if not ico_png_layers(data):
            print(f"  {path.relative_to(ROOT)} đã đúng")
            continue
        layers = []
        for size, body in ico_entries(data):
            if body[:8] != b"\x89PNG\r\n\x1a\n":
                raise SystemExit(f"{path}: lớp {size} không phải PNG, không tự sửa được")
            layers.append((size, png_rgba(body)[1], body))
        path.write_bytes(ico_bytes(layers))
        print(f"  {path.relative_to(ROOT)} ghi lại: lớp BMP {[s for s, _, _ in layers if s < 256]}, PNG [256]")
    return check()


def check() -> int:
    errors: list[str] = []
    if not SOURCE.is_file() or sha256(SOURCE) != SOURCE_SHA256:
        errors.append(f"ảnh gốc thiếu hoặc khác SOURCE_SHA256: {SOURCE.relative_to(ROOT)}")
    for path, size in OUTPUTS.items():
        rel = path.relative_to(ROOT)
        if not path.is_file():
            errors.append(f"thiếu {rel}")
            continue
        data = path.read_bytes()
        if path.suffix == ".ico":
            if ico_sizes(data) != ICO_SIZES:
                errors.append(f"{rel}: lớp {ico_sizes(data)}, cần {ICO_SIZES}")
            if path.parent == APP_ICONS and ico_png_layers(data):
                errors.append(f"{rel}: lớp {ico_png_layers(data)} là PNG, phải là BMP (chạy --sua-ico)")
        elif path.suffix == ".png" and size and png_size(data) != size:
            errors.append(f"{rel}: cỡ {png_size(data)}, cần {size}")
    for e in errors:
        print("LỖI", e)
    print("ĐẠT" if not errors else f"{len(errors)} lỗi")
    return 1 if errors else 0


# ---------------------------------------------------------------------------------------------------------------- sinh

def build() -> None:
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    from scipy import ndimage

    if sha256(SOURCE) != SOURCE_SHA256:
        sys.exit("Ảnh gốc khác SOURCE_SHA256: đổi logo thì cập nhật chuỗi băm trong script này (có chủ đích).")

    # 1. Cắt con cú khỏi ảnh gốc, tách nền tím nhạt (chuyển sắc) của ảnh
    src = Image.open(SOURCE).convert("RGB").crop(OWL_BOX)
    c = np.asarray(src).astype(np.float64)
    h, w, _ = c.shape
    ys, xs = np.mgrid[0:h, 0:w]
    edge = np.zeros((h, w), bool)
    edge[:4], edge[-4:], edge[:, :4], edge[:, -4:] = True, True, True, True
    plane = np.c_[xs[edge], ys[edge], np.ones(edge.sum())]
    grid = np.c_[xs.ravel(), ys.ravel(), np.ones(h * w)]
    bg = np.stack([(grid @ np.linalg.lstsq(plane, c[..., k][edge], rcond=None)[0]).reshape(h, w) for k in range(3)], -1)
    dist = np.sqrt(((c - bg) ** 2).sum(axis=2))        # khoảng cách tới nền tại chỗ
    lo, hi = 8, 40
    near, _ = ndimage.label(dist < hi)                  # nền = vùng gần màu nền NỐI với mép ảnh
    outer = np.unique(np.r_[near[0], near[-1], near[:, 0], near[:, -1]])
    background = np.isin(near, outer[outer > 0])        # (lòng trắng mắt cú gần màu nền nhưng không nối mép -> giữ)
    alpha = np.where(background, np.clip((dist - lo) / (hi - lo), 0, 1), 1.0)
    rgb = np.clip((c - bg * (1 - alpha[..., None])) / np.maximum(alpha, 1e-6)[..., None], 0, 255)
    solid = alpha > 0.3                                 # bỏ mảnh lạc (chữ bên cạnh): giữ khối lớn nhất + quầng 6 px
    parts, n = ndimage.label(solid)
    main = 1 + int(np.argmax(ndimage.sum(solid, parts, range(1, n + 1))))
    alpha = np.where(ndimage.binary_dilation(parts == main, iterations=6), alpha, 0)
    rgba = np.dstack([rgb, alpha * 255]).round().astype(np.uint8)
    rgba[alpha * 255 < 1.5] = 0
    cut = Image.fromarray(rgba, "RGBA")

    def bbox(img: Image.Image, threshold: int) -> tuple[int, int, int, int]:
        return img.getchannel("A").point(lambda v: 255 if v > threshold else 0).getbbox()

    def square(img: Image.Image, margin: float) -> Image.Image:
        side = max(img.size)
        pad = round(side * margin)
        out = Image.new("RGBA", (side + 2 * pad,) * 2, (0, 0, 0, 0))
        out.alpha_composite(img, ((out.width - img.width) // 2, (out.height - img.height) // 2))
        return out

    def resize(img: Image.Image, size: tuple[int, int]) -> Image.Image:
        return img.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")

    tight = cut.crop(bbox(cut, 2))                      # hình + quầng, sát mép
    master = square(tight, 0.03)                        # bản chính vuông
    core = square(cut.crop(bbox(cut, 96)), 0.0)         # chỉ phần đặc, cho cỡ nhỏ

    def icon(size: int) -> Image.Image:
        if size <= SMALL:
            img = resize(core, (size, size))
            return img.filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=0))
        img = resize(master, (size, size))
        if size > master.width:                         # phóng to (xem bước 4)
            img = img.filter(ImageFilter.UnsharpMask(radius=1.0 * size / master.width, percent=45, threshold=1))
        return img

    def on(bg: tuple[int, int, int], size: int, scale: float) -> Image.Image:
        out = Image.new("RGBA", (size, size), bg + (255,))
        inner = round(size * scale)
        out.alpha_composite(icon(inner), ((size - inner) // 2, (size - inner) // 2))
        return out.convert("RGB")

    def save(img: Image.Image, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.suffix == ".png":
            img.save(path, optimize=True)
        elif path.suffix == ".webp":
            img.save(path, quality=90, alpha_quality=95, method=6)
        elif path.suffix == ".avif":
            img.save(path, quality=80, speed=2)
        print(f"  {path.relative_to(ROOT)} {img.size[0]}x{img.size[1]} {path.stat().st_size // 1024} KiB")

    def save_ico(path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.parent != APP_ICONS:                    # favicon: trình duyệt đọc lớp PNG, tệp nhỏ hơn
            frames = [icon(s) for s in ICO_SIZES]
            frames[-1].save(path, format="ICO", sizes=[(s, s) for s in ICO_SIZES], append_images=frames[:-1])
            print(f"  {path.relative_to(ROOT)} lớp {ico_sizes(path.read_bytes())}")
            return
        layers = []
        for size in ICO_SIZES:
            frame = icon(size).convert("RGBA")
            png = io.BytesIO()
            frame.save(png, format="PNG", optimize=True)
            layers.append((size, frame.tobytes(), png.getvalue()))
        path.write_bytes(ico_bytes(layers))             # không dùng Pillow ghi ICO: nó ghi mọi lớp thành PNG
        print(f"  {path.relative_to(ROOT)} lớp {ico_sizes(path.read_bytes())}")

    print("Bản chính:", master.size)
    save(master, BRAND / "tsufiles-mark.png")
    save(master, BRAND / "tsufiles-mark.webp")
    save(master, BRAND / "tsufiles-mark.avif")
    save(icon(512), BRAND / "tsufiles-mark-512.png")

    # Ứng dụng desktop, Setup, CLI, gói Linux/macOS, gói menu Explorer (đều đọc hai tệp này)
    save(icon(512), APP_ICONS / "tsufiles.png")
    save_ico(APP_ICONS / "tsufiles.ico")

    # Website files.tsudev.com
    save_ico(WEB / "favicon.ico")
    for s in (16, 32, 96):
        save(icon(s), WEB / f"favicon-{s}x{s}.png")
    save(on(WEB_DARK, 180, 0.82), WEB / "apple-touch-icon.png")
    for s in (192, 512):
        save(icon(s), WEB / f"android-chrome-{s}x{s}.png")
        save(on(WEB_DARK, s, 0.62), WEB / f"android-chrome-maskable-{s}x{s}.png")  # trong vùng an toàn 80 %
    header = resize(tight, (round(tight.width * 168 / tight.height), 168))         # đầu trang: cao 42 px, 4x
    for ext in ("png", "webp", "avif"):
        save(header, WEB / f"brand/tsufiles-mark.{ext}")
    save(og_image(tight, resize, Image, ImageDraw), WEB / "og-image.png")


def og_image(tight, resize, Image, ImageDraw):
    """Ảnh chia sẻ 1200x630, nền và chữ theo token chế độ Tối của website."""
    from fontTools.ttLib import TTFont
    from PIL import ImageFont

    W, H = 1200, 630
    # website/src/styles/palette.css, [data-theme="dark"]
    BG, SURFACE, BORDER = (0x0A, 0x0A, 0x0A), (0x11, 0x11, 0x11), (0x2E, 0x2E, 0x2E)
    TEXT, TEXT2 = (0xED, 0xED, 0xED), (0xB4, 0xB4, 0xB4)

    class Font:
        """Be Vietnam Pro tự host chia theo bộ ký tự: chọn tệp có glyph cho từng ký tự."""

        def __init__(self, weight: int, size: int):
            self.parts = []
            for subset in ("latin", "vietnamese", "latin-ext"):
                path = WEB_FONTS / f"be-vietnam-pro-{weight}-{subset}.woff2"
                cmap = TTFont(path).getBestCmap()
                self.parts.append((set(cmap), ImageFont.truetype(str(path), size)))

        def pick(self, ch: str):
            return next((f for cmap, f in self.parts if ord(ch) in cmap), self.parts[0][1])

        def width(self, text: str) -> float:
            return sum(self.pick(ch).getlength(ch) for ch in text)

        def draw(self, d, xy, text: str, fill) -> None:
            x, y = xy
            for ch in text:
                f = self.pick(ch)
                d.text((x, y), ch, font=f, fill=fill, anchor="ls")
                x += f.getlength(ch)

    # Lớp bản sắc tsudev-standards (products/files.json, chế độ Tối vì ảnh chia sẻ nền tối): không chép tay mã màu.
    import json
    product = json.loads((ROOT / ".standards/tokens/products/files.json").read_text(encoding="utf-8"))
    hexrgb = lambda h: tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
    PRIMARY = hexrgb(product["colors"]["dark"]["primary"])
    ON_PRIMARY = hexrgb(product["colors"]["dark"]["on-primary"])
    ACCENT = hexrgb(product["colors"]["dark"]["accent"])

    og = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(og)
    d.rounded_rectangle((48, 48, W - 48, H - 48), radius=32, fill=SURFACE, outline=BORDER, width=2)
    if product.get("motif") == "quy-dao":
        # Hoạ tiết `quy-dao` (DESIGN_SYSTEM 1.2.4): vòng quỹ đạo tĩnh quanh con cú, nét accent mờ (<= 15% độ đậm).
        S = 2   # vẽ gấp đôi rồi thu nhỏ cho nét mượt
        lop = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
        ld = ImageDraw.Draw(lop)
        cx, cy = 290 * S, 315 * S
        for rx, ry, a in ((250, 120, 40), (330, 160, 30), (420, 205, 22)):
            ld.ellipse((cx - rx * S, cy - ry * S, cx + rx * S, cy + ry * S), outline=ACCENT + (a,), width=2 * S)
        lop = lop.rotate(12, resample=Image.BICUBIC, center=(cx, cy))
        ld = ImageDraw.Draw(lop)
        for x, y, r in ((540, 220, 9), (95, 420, 7), (470, 470, 6), (150, 150, 5)):
            ld.ellipse(((x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S), fill=ACCENT + (38,))
        lop = lop.resize((W, H), Image.LANCZOS)
        # Chỉ trong khung thẻ (không tràn ra lề ngoài)
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).rounded_rectangle((50, 50, W - 50, H - 50), radius=30, fill=255)
        # Nhân alpha với mặt nạ (paste qua mặt nạ lấy từ chính alpha sẽ nhân alpha hai lần - nét gần như biến mất)
        from PIL import ImageChops
        lop.putalpha(ImageChops.multiply(lop.getchannel("A"), mask))
        og = Image.alpha_composite(og.convert("RGBA"), lop).convert("RGB")
        d = ImageDraw.Draw(og)
    mh = 340
    mark = resize(tight, (round(tight.width * mh / tight.height), mh))
    og.paste(mark, (120, (H - mh) // 2), mark)
    x = 120 + mark.width + 56
    Font(700, 64).draw(d, (x, 250), "TsuFiles", TEXT)
    reg = Font(400, 30)
    reg.draw(d, (x, 312), "Chuyển đổi tệp ngoại tuyến, kéo thả là xong", TEXT2)
    reg.draw(d, (x, 356), "Hơn 260 định dạng - tiếng Việt, mã nguồn mở", TEXT2)
    semi = Font(600, 22)
    label = "files.tsudev.com"
    bw = semi.width(label) + 56
    d.rounded_rectangle((x, 400, x + bw, 456), radius=28, fill=PRIMARY)
    semi.draw(d, (x + 28, 436), label, ON_PRIMARY)
    Font(600, 30).draw(d, (W - 110 - Font(600, 30).width("TsuDEV"), H - 100), "TsuDEV", TEXT)
    return og


if __name__ == "__main__":
    if "--kiem" in sys.argv[1:]:
        sys.exit(check())
    if "--sua-ico" in sys.argv[1:]:
        sys.exit(fix_icos())
    build()
    sys.exit(check())
