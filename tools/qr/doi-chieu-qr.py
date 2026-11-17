#!/usr/bin/env python3
"""Sinh dòng [InlineData] đối chiếu cho tests/TsuFiles.Core.Tests/QrCodeTests.cs (ADR-0004 S2).

Dùng segno làm bộ mã hóa tham chiếu, ép cùng phiên bản và mặt nạ với QrCode.cs, rồi in SHA-256 của ma trận
(mỗi hàng là chuỗi 0/1, nối bằng '\\n'). Chạy tay, không chạy trong CI:

  python3 -m venv /tmp/qr && /tmp/qr/bin/pip install -r tools/ung-ho/requirements.txt
  /tmp/qr/bin/python -I tools/qr/doi-chieu-qr.py

Lưu ý: segno 1.6.6 luôn thêm 8 bit 0 khi chuỗi bit đã tròn byte (`8 - len % 8`), trái ISO/IEC 18004:2015 mục
7.4.10 (chỉ đệm tới ranh giới byte). Ở chế độ byte chuỗi bit luôn tròn byte nên mọi mã chưa đầy sẽ lệch một byte
đệm. Script vá đúng hàm đó theo chuẩn trước khi sinh; mọi bước khác là của segno.
"""
from __future__ import annotations

import hashlib

import segno
import segno.encoder


def _padding_theo_chuan(buff, version, length):
    buff.extend([0] * ((8 - length % 8) % 8))


segno.encoder.write_padding_bits = _padding_theo_chuan

# (số byte, mặt nạ): phủ phiên bản 1-40, đủ 8 mặt nạ, mã đầy và chưa đầy, nhiều khối ngắn/dài.
CASES = [
    (1, 0), (14, 6), (15, 7), (16, 1), (30, 2), (60, 3), (100, 4), (154, 5), (200, 0), (213, 7),
    (300, 4), (400, 6), (500, 1), (700, 2), (1000, 3), (1300, 5), (1700, 6), (2000, 0), (2331, 7),
]


def du_lieu(n: int) -> bytes:
    # Trùng với QrCodeTests.Data(n).
    return bytes((i * 31 + 7) & 0xFF for i in range(n))


def main() -> None:
    for n, mask in CASES:
        qr = segno.make(du_lieu(n), error="m", mode="byte", boost_error=False, mask=mask, micro=False)
        rows = "\n".join("".join("1" if c else "0" for c in row) for row in qr.matrix)
        digest = hashlib.sha256(rows.encode("ascii")).hexdigest()
        print(f'    [InlineData({n}, {mask}, {qr.version}, "{digest}")]')


if __name__ == "__main__":
    main()
