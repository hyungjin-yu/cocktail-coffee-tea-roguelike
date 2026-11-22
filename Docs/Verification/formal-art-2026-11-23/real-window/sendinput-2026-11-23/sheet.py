# contact sheet: sheet.py dir out step cols  (frames labelled with ms from first)
import sys, os, glob
from PIL import Image, ImageDraw
d, out, step, cols = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
start = int(sys.argv[5]) if len(sys.argv) > 5 else 0
fs = sorted(glob.glob(os.path.join(d, 'f*.jpg')))[start::step]
tw, th = 426, 240
rows = (len(fs) + cols - 1) // cols
S = Image.new('RGB', (cols*tw, rows*(th+18)), 'black'); D = ImageDraw.Draw(S)
for i, f in enumerate(fs):
    im = Image.open(f).resize((tw, th)); x, y = (i % cols)*tw, (i//cols)*(th+18)
    S.paste(im, (x, y+18)); n = int(os.path.basename(f)[1:4]) - 1
    D.text((x+4, y+3), f'{os.path.basename(f)}  t={n*1000/60:.0f}ms', fill='yellow')
S.save(out, quality=88); print(len(fs), S.size)
