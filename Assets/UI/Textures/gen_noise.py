"""生成 UI 背景噪点纹理 PNG（纯标准库，无需 Pillow / numpy）。

目标底色：rgb(11, 13, 16)，即 ui-tokens.uss 里的 .bg-base
用法：直接运行会在同目录生成 noise-weak.png / noise-mid.png / noise-strong.png
然后把想要的那一张拖进 Unity 的 Assets/UI/Textures/
"""
import os
import random
import struct
import zlib

W = H = 512
BASE_R, BASE_G, BASE_B = 11, 13, 16
OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def chunk(tag: bytes, data: bytes) -> bytes:
    return (struct.pack(">I", len(data)) + tag + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))


def write_png(path: str, amp: int, seed: int) -> int:
    random.seed(seed)
    rows = bytearray()
    for _ in range(H):
        rows.append(0)  # PNG filter type 0 (None)
        for _ in range(W):
            r = max(0, min(255, BASE_R + random.randint(-amp, amp)))
            g = max(0, min(255, BASE_G + random.randint(-amp, amp)))
            b = max(0, min(255, BASE_B + random.randint(-amp, amp)))
            rows.extend((r, g, b))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(rows), 9))
    png += chunk(b"IEND", b"")

    with open(path, "wb") as f:
        f.write(png)
    return os.path.getsize(path)


if __name__ == "__main__":
    for name, amp, seed in [
        ("noise-weak.png", 2, 101),
        ("noise-mid.png", 4, 202),
        ("noise-strong.png", 8, 303),
    ]:
        size = write_png(os.path.join(OUT_DIR, name), amp, seed)
        print(f"{name:20s} amp=±{amp:<2d} {size/1024:7.1f} KB")
