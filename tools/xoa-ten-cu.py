#!/usr/bin/env python3
"""TsuFiles: bỏ tên sản phẩm cũ "Filee" khỏi chữ hiển thị, chú thích và tài liệu (ADR-0003 mục 8, 16/11/2026).

Sau T1-T4 (namespace, tệp chạy, thư mục dữ liệu, repo đã là TsuFiles), tên cũ còn trong bản dịch, chú thích mã, tiêu
đề cửa sổ, thông báo lỗi và tài liệu. Script này đổi chữ "Filee" đứng riêng thành "TsuFiles" ở những chỗ đó và đổi tên
tệp icon `filee.ico`/`filee.png` thành `tsufiles.ico`/`tsufiles.png`.

GIỮ NGUYÊN (bắt buộc - không sửa tay để "sạch" thêm):
- `LICENSE`, `THIRD-PARTY-NOTICES.md`: thông báo bản quyền MIT của tác giả gốc (giấy phép bắt buộc giữ trong mọi bản
  sao; xóa đi là vi phạm bản quyền). Lịch sử: `logs/`, `CHANGELOG.md`, `docs/adr/`.
- Định danh lúc chạy mà bản đã cài còn dùng (ADR-0003 mục 5, 6): `Filee.exe`, `Filee.app`, `Filee.lnk`, tiền tố tệp
  phát hành `Filee-<ver>-`, `Filee.ExplorerMenu`/`FileeExplorerMenu`/`CN=Filee`, `com.filee.app`, `.filee-package`,
  `.filee-component`, `X-Filee-Managed`, `filee-convert`, `~/.local/share/filee/...`, giá trị registry `Filee Setup:`,
  AppId Inno Setup. Dòng nào nhắc tới chúng (hay mô tả việc di trú bản cũ) được bỏ qua nguyên dòng.

Dùng:
  python3 -I tools/xoa-ten-cu.py          sửa
  python3 -I tools/xoa-ten-cu.py --kiem   chỉ đếm chỗ còn sửa được (thoát 1 nếu còn)
"""

import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent

SKIP_PREFIXES = (".standards/", "logs/", "docs/adr/", "CHANGELOG.md", "LICENSE", "THIRD-PARTY-NOTICES.md",
                 "tools/doi-ten-tsudev.py", "tools/xoa-ten-cu.py", ".standards-upstream", ".gitleaksignore",
                 "docs/SECURITY.upstream.md", "src/Filee.ExplorerMenu/",
                 "tests/")  # test giữ dữ liệu mẫu/tên cũ cố ý (di trú, Brand(), tên tệp phát hành); sửa tay khi cần
TEXT_SUFFIXES = {".cs", ".csproj", ".props", ".targets", ".axaml", ".xaml", ".json", ".xml", ".yml", ".yaml", ".ps1",
                 ".sh", ".py", ".md", ".txt", ".plist", ".js", ".mjs", ".ts", ".astro", ".toml", ".css", ".html", ".svg",
                 ".iss", ".desktop", ".in"}

# Dòng nói về định danh lúc chạy của bản cũ / di trú: giữ nguyên cả dòng.
PROTECTED = re.compile(
    r"Filee(?:\.exe|\.app|\.lnk|\.iss|\.dll|\.cmd|\.ExplorerMenu|ExplorerMenu|-\d|-test-|-<|\.Setup\b| Setup:|_)"
    r"|CN=Filee|com\.filee|\.filee-|filee-convert|X-Filee|share/filee|/opt/Filee|/apps/Filee|filee\.sh"
    r"|AppId|AppGuid|Inno Setup|\b2\.4\.0\b|[Ll]egacy|[Uu]pstream|KnifeLemon|Myeonggu|[Cc]opyright|PreviousInstall"
    r"|\(\?:Filee|Filee\|"
    # Câu lịch sử: phiên bản của bản gốc ("Filee 2.1.0"), chuỗi đổi tên ("Filee" -> ...), "Filee gốc", tên cũ "Filee".
    r"|Filee \d|\"Filee\" ->|Filee gốc|cũ \"Filee\""
    # Định danh trong mã (khóa font, tên ứng dụng Avalonia, Id gói, thư mục cài cũ, launcher Linux), không phải chữ hiển thị.
    r"|[\"']Filee[\"'#/\\]|fonts:Filee|\"Id\", \"Filee\"|LocalAppData, \"Filee\"|\$bin 'Filee'|^\s*\"Filee\",$"
    r"|copied - 1;")

# "Filee" là một từ (không phải một phần của đường dẫn, tên tệp hay định danh); "\nFilee" trong chuỗi JSON cũng tính.
WORD = re.compile(r"(?:(?<=\\n)|(?<![\w./\\@-]))Filee(?![\w-]|\.[A-Za-z])")
ICON = re.compile(r"\bfilee\.(ico|png)\b")
ICON_FILES = {"src/TsuFiles.App/Assets/Icons/filee.ico": "src/TsuFiles.App/Assets/Icons/tsufiles.ico",
              "src/TsuFiles.App/Assets/Icons/filee.png": "src/TsuFiles.App/Assets/Icons/tsufiles.png"}


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True).stdout


def eligible(path: str) -> bool:
    return not path.startswith(SKIP_PREFIXES) and pathlib.Path(path).suffix.lower() in TEXT_SUFFIXES


def rewrite(text: str) -> tuple[str, int]:
    count = 0
    lines = text.split("\n")
    for i, line in enumerate(lines):
        if "ilee" not in line or PROTECTED.search(line):
            continue
        new, n = WORD.subn("TsuFiles", line)
        new, m = ICON.subn(lambda match: "tsufiles." + match.group(1), new)
        if n or m:
            lines[i] = new
            count += n + m
    return "\n".join(lines), count


def main() -> int:
    check = "--kiem" in sys.argv[1:]
    total = 0
    for path in [p for p in git("ls-files", "-z").split("\0") if p and eligible(p)]:
        file = ROOT / path
        try:
            text = file.read_text(encoding="utf-8")
        except (UnicodeDecodeError, FileNotFoundError):
            continue
        new, count = rewrite(text)
        if count:
            total += count
            if check:
                print(f"{path}: {count}")
            else:
                file.write_text(new, encoding="utf-8")
    if not check:
        for old, new in ICON_FILES.items():
            if (ROOT / old).exists():
                git("mv", old, new)
    print(f"{'Còn' if check else 'Đã đổi'} {total} chỗ")
    return 1 if check and total else 0


if __name__ == "__main__":
    sys.exit(main())
