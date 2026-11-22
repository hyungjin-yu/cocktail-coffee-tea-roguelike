# Driver: focus exact game window, SendInput (scancode) keys / mouse, screenshots. Aborts if STOP exists.
import ctypes, ctypes.wintypes as W, sys, time, os, json
QA = os.path.dirname(os.path.abspath(__file__))
STOP = os.path.join(QA, 'STOP')
LOG = os.path.join(QA, 'drv.log')
u32 = ctypes.WinDLL('user32', use_last_error=True)
k32 = ctypes.WinDLL('kernel32')
u32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
PROC = 'CocktailCoffeeTea.exe'
SC = {'1':0x02,'2':0x03,'3':0x04,'4':0x05,'w':0x11,'a':0x1E,'s':0x1F,'d':0x20,'esc':0x01,'enter':0x1C,'space':0x39,
      'b':0x30,'t':0x14,'r':0x13,'tab':0x0F,'e':0x12,'q':0x10,'f':0x21}
def log(s):
    with open(LOG, 'a', encoding='utf-8') as f: f.write(f"{time.strftime('%H:%M:%S')}.{int(time.time()*1000)%1000:03d} {s}\n")
def guard():
    if os.path.exists(STOP):
        print('ABORT: user input detected ->', open(STOP).read().strip()); log('ABORT stop file'); sys.exit(3)
def find():
    import subprocess
    pids = set()
    out = subprocess.run(['tasklist','/FI',f'IMAGENAME eq {PROC}','/FO','CSV','/NH'],capture_output=True,text=True).stdout
    for line in out.splitlines():
        p = line.split('","')
        if len(p) > 1: pids.add(int(p[1]))
    res = []
    EnumProc = ctypes.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
    def cb(h, l):
        pid = W.DWORD(); u32.GetWindowThreadProcessId(h, ctypes.byref(pid))
        if pid.value in pids and u32.IsWindowVisible(h):
            buf = ctypes.create_unicode_buffer(256); u32.GetWindowTextW(h, buf, 256)
            res.append((h, buf.value))
        return True
    u32.EnumWindows(EnumProc(cb), 0)
    return res
def hwnd():
    r = find()
    if not r: print('no game window'); sys.exit(2)
    return r[0][0]
def rect(h):
    r = W.RECT(); u32.GetClientRect(h, ctypes.byref(r))
    p = W.POINT(0,0); u32.ClientToScreen(h, ctypes.byref(p))
    return p.x, p.y, r.right, r.bottom
def focus(h):
    if u32.GetForegroundWindow() == h: return True
    # ALT tap trick allows SetForegroundWindow from background process
    send_key_raw(0x38, False, ext=False); send_key_raw(0x38, True)
    u32.ShowWindow(h, 9); u32.SetForegroundWindow(h); time.sleep(0.25)
    return u32.GetForegroundWindow() == h
class KI(ctypes.Structure):
    _fields_=[('wVk',W.WORD),('wScan',W.WORD),('dwFlags',W.DWORD),('time',W.DWORD),('extra',ctypes.c_size_t)]
class MI(ctypes.Structure):
    _fields_=[('dx',ctypes.c_long),('dy',ctypes.c_long),('data',W.DWORD),('dwFlags',W.DWORD),('time',W.DWORD),('extra',ctypes.c_size_t)]
class U(ctypes.Union): _fields_=[('ki',KI),('mi',MI),('pad',ctypes.c_byte*32)]
class INP(ctypes.Structure): _fields_=[('type',W.DWORD),('u',U)]
def send_key_raw(sc, up, ext=False):
    i = INP(); i.type = 1; i.u.ki = KI(0, sc, 0x0008 | (0x0002 if up else 0) | (0x0001 if ext else 0), 0, 0)
    n = u32.SendInput(1, ctypes.byref(i), ctypes.sizeof(INP)); return n
def ensure(h):
    guard()
    if not focus(h):
        print('FOCUS FAIL fg=', u32.GetForegroundWindow(), 'game=', h); log('focus fail'); sys.exit(4)
def key(h, name, hold_ms=60):
    ensure(h); sc = SC[name]
    send_key_raw(sc, False); log(f'down {name}'); time.sleep(hold_ms/1000)
    ensure(h) if hold_ms > 400 else None
    send_key_raw(sc, True); log(f'up {name} held={hold_ms}')
def click(h, cx, cy):
    ensure(h); x, y, w, hh = rect(h)
    sw, sh = u32.GetSystemMetrics(0), u32.GetSystemMetrics(1)
    ax = int((x+cx)*65535/(sw-1)); ay = int((y+cy)*65535/(sh-1))
    for fl in (0x8001, 0x8002, 0x8004):
        i = INP(); i.type = 0; i.u.mi = MI(ax, ay, 0, fl, 0, 0); u32.SendInput(1, ctypes.byref(i), ctypes.sizeof(INP)); time.sleep(0.05)
    log(f'click {cx},{cy}')
def shot(h, path):
    from PIL import ImageGrab
    x, y, w, hh = rect(h)
    ImageGrab.grab(bbox=(x, y, x+w, y+hh), all_screens=True).save(path, quality=88); log(f'shot {path}')
if __name__ == '__main__':
    a = sys.argv[1:]; c = a[0]
    if c == 'find': print(find()); sys.exit(0)
    h = hwnd()
    if c == 'info': print('hwnd', h, 'client', rect(h), 'fg', u32.GetForegroundWindow() == h)
    elif c == 'focus': guard(); print('focused', focus(h))
    elif c == 'key': key(h, a[1], int(a[2]) if len(a) > 2 else 60)
    elif c == 'down': ensure(h); send_key_raw(SC[a[1]], False); log(f'down {a[1]}')
    elif c == 'up': send_key_raw(SC[a[1]], True); log(f'up {a[1]}')
    elif c == 'click': click(h, int(a[1]), int(a[2]))
    elif c == 'shot': shot(h, a[1])
    elif c == 'seq':
        # seq "key:2 wait:300 down:w wait:1500 up:w shot:path"
        for t in a[1].split():
            k, _, v = t.partition(':')
            if k == 'key': key(h, v)
            elif k == 'down': ensure(h); send_key_raw(SC[v], False); log(f'down {v}')
            elif k == 'up': send_key_raw(SC[v], True); log(f'up {v}')
            elif k == 'wait':
                end = time.time() + int(v)/1000
                while time.time() < end:
                    if os.path.exists(STOP):
                        for s in SC.values(): send_key_raw(s, True)
                        guard()
                    time.sleep(0.02)
            elif k == 'click': x_, y_ = v.split(','); click(h, int(x_), int(y_))
            elif k == 'shot': shot(h, v)
    print('ok')
