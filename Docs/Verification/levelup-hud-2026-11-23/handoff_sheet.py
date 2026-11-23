# Contact sheet of consecutive 60 fps render frames (LevelUpHandoffSequenceCapture).
# usage: python -I handoff_sheet.py <frames_dir> <tag_WxH> <csv> <out.jpg> <crop: x0,y0,x1,y1 as fraction of frame> [cols] [step]
import sys, os, glob, csv
from PIL import Image, ImageDraw
fdir, tag, csvp, out, crop = sys.argv[1:6]
cols = int(sys.argv[6]) if len(sys.argv) > 6 else 6
step = int(sys.argv[7]) if len(sys.argv) > 7 else 1
cx0, cy0, cx1, cy1 = [float(v) for v in crop.split(',')]
rows = {}
with open(csvp, encoding='utf-8') as f:
    for r in csv.DictReader(f): rows.setdefault(int(r['frame']), []).append(r)
def label(n):
    rs = rows.get(n, [])
    if not rs: return f'f{n}'
    r0 = rs[0]
    if 'slot' in r0:
        parts = [f"s{r['slot']}:T{r['tier']}/{r['clip_frame']}" + ('' if r['reveal'] == 'playing' else '*') for r in rs]
        inter = any(r['interactive'] == 'True' for r in rs)
    else:
        parts = [f"T{r0['tier']}/{r0['clip_frame']} a{r0['content_alpha']}"]
        inter = r0['interactive'] == 'True'
    return f"f{n:03d} {r0['t_ms']}ms " + ' '.join(parts) + (' INPUT' if inter else '')
fs = sorted(glob.glob(os.path.join(fdir, f'{tag}_f*.png')))[::step]
first = Image.open(fs[0]); W, H = first.size
box = (int(cx0 * W), int(cy0 * H), int(cx1 * W), int(cy1 * H))
tw = 420; th = int(tw * (box[3] - box[1]) / (box[2] - box[0]))
nrows = (len(fs) + cols - 1) // cols
S = Image.new('RGB', (cols * tw, nrows * (th + 16)), 'black'); D = ImageDraw.Draw(S)
for i, p in enumerate(fs):
    n = int(os.path.basename(p).rsplit('_f', 1)[1][:3])
    im = Image.open(p).convert('RGB').crop(box).resize((tw, th), Image.NEAREST)
    x, y = (i % cols) * tw, (i // cols) * (th + 16)
    S.paste(im, (x, y + 16)); D.text((x + 3, y + 2), label(n), fill='yellow')
S.save(out, quality=85); print(out, len(fs), S.size)
