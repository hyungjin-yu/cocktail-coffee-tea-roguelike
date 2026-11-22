# scan video: print onset times (s) where card-window title appears. usage: scan.py video fps [start dur]
import subprocess, sys, numpy as np
v, fps = sys.argv[1], sys.argv[2]
extra = ['-ss', sys.argv[3], '-t', sys.argv[4]] if len(sys.argv) > 4 else []
base = float(sys.argv[3]) if extra else 0.0
W, H = 640, 360
p = subprocess.Popen(['ffmpeg','-nostdin','-v','error'] + extra + ['-i', v, '-vf', f'fps={fps},scale={W}:{H}', '-f','rawvideo','-pix_fmt','rgb24','-'], stdout=subprocess.PIPE)
i = 0; prev = False; out = []
while True:
    b = p.stdout.read(W*H*3)
    if len(b) < W*H*3: break
    a = np.frombuffer(b, np.uint8).reshape(H, W, 3)
    t = (a[15:35, 215:425] > 215).all(axis=2).sum()
    on = t > 120
    if on != prev: print(f'{base + i/float(fps):.3f} {"OPEN" if on else "close"} {t}')
    prev = on; i += 1
print('frames', i)
