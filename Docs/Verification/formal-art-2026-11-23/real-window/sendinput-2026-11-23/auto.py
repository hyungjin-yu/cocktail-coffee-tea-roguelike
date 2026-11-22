# Move until a card window opens (or timeout). Optional: hold a key (e.g. 1) during movement.
# usage: auto.py <tag> <timeout_s> [holdkey]
import sys, time, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import drv
from PIL import ImageGrab
import numpy as np
tag, timeout = sys.argv[1], float(sys.argv[2]); hold = sys.argv[3] if len(sys.argv) > 3 else None
h = drv.hwnd()
def grab():
    x, y, w, hh = drv.rect(h); return ImageGrab.grab(bbox=(x, y, x+w, y+hh), all_screens=True)
def state(img):
    a = np.asarray(img.convert('RGB')).astype(int)
    title = ((a[30:70, 430:560] > 200).all(axis=2)).sum() * 3
    pink = ((a[70:130, 480:800, 0] > 200) & (a[70:130, 480:800, 1] < 140)).sum()
    global lastv; lastv=(title,pink)
    return 'card' if title > 600 else ('dead' if pink > 800 else 'play')
pat = [('d',), ('d','s'), ('s',), ('s','a'), ('a',), ('a','w'), ('w',), ('w','d')] if os.environ.get('PAT') != 'sq' else [('d',), ('s',), ('a',), ('w',)]
i = 0; t0 = time.time(); held = []
if hold: drv.ensure(h); drv.send_key_raw(drv.SC[hold], False); drv.log(f'HOLD down {hold}')
try:
    while time.time() - t0 < timeout:
        dirs = pat[i % len(pat)]; i += 1
        drv.ensure(h)
        for k in dirs: drv.send_key_raw(drv.SC[k], False)
        end = time.time() + float(os.environ.get('SEG','0.8'))
        st = 'play'
        while time.time() < end:
            drv.guard(); img = grab(); st = state(img)
            if st != 'play': break
        for k in dirs: drv.send_key_raw(drv.SC[k], True)
        if i % 4 == 0: img.save(os.path.join(drv.QA, f'dbg_step{i:03d}.jpg'))
        drv.log(f'step {i} {dirs} {st} {lastv}')
        if st != 'play':
            ts = time.strftime('%H:%M:%S') + f'.{int(time.time()*1000)%1000:03d}'
            p = os.path.join(drv.QA, f'{tag}_{st}.jpg'); img.save(p, quality=90)
            drv.log(f'DETECT {st} at {ts}'); print('DETECT', st, ts, p); break
    else:
        print('timeout')
finally:
    if hold and os.path.exists(drv.STOP): drv.send_key_raw(drv.SC[hold], True)
