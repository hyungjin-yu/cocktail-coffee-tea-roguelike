"""Opaque bounds of a non-interlaced 8-bit PNG (RGBA or palette+tRNS), pure Python (no Pillow) — Unity owner import helper.
Returns (x0, y0, x1, y1) in pixel coords with y measured from the TOP, x1/y1 exclusive; None if fully transparent."""
import struct, zlib


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    return a if pa <= pb and pa <= pc else (b if pb <= pc else c)


def alpha_bounds(path):
    data = open(path, "rb").read()
    assert data[:8] == bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), "not png"
    pos, idat, trns = 8, b"", None
    w = h = depth = ctype = interlace = None
    while pos < len(data):
        n, typ = struct.unpack(">I4s", data[pos:pos + 8]); body = data[pos + 8:pos + 8 + n]; pos += 12 + n
        if typ == b"IHDR": w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
        elif typ == b"IDAT": idat += body
        elif typ == b"tRNS": trns = body
        elif typ == b"IEND": break
    assert depth == 8 and interlace == 0 and ctype in (6, 3), f"unsupported png {path}: depth={depth} type={ctype} interlace={interlace}"
    bpp = 4 if ctype == 6 else 1
    raw = zlib.decompress(idat)
    stride = w * bpp
    prev = bytearray(stride); rows = []
    i = 0
    for _ in range(h):
        f = raw[i]; line = bytearray(raw[i + 1:i + 1 + stride]); i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]; c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4: line[x] = (line[x] + _paeth(a, b, c)) & 255
        rows.append(line); prev = line
    x0, y0, x1, y1 = w, h, -1, -1
    for y, line in enumerate(rows):
        for x in range(w):
            alpha = line[x * 4 + 3] if ctype == 6 else (trns[line[x]] if trns is not None and line[x] < len(trns) else 255)
            if alpha > 0:
                x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
    return None if x1 < 0 else (x0, y0, x1 + 1, y1 + 1)
